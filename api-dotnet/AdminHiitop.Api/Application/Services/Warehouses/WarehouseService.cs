using AdminHiitop.Api.Application.DTOs.Shopify;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Application.Options;
using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Domain.Shopify.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using AdminHiitop.Api.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AdminHiitop.Api.Application.Services.Warehouses;

public sealed class WarehouseService : IWarehouseService
{
    private readonly ICatalogQueryService _catalogQueryService;
    private readonly AdminHiitopDbContext _context;
    private readonly IShopifyProductService _shopifyProductService;
    private readonly PosOptions _posOptions;

    // IDs >= this threshold are Shopify location rows; subtract to get local DB id.
    private const int ShopifyIdOffset = 100_000;

    public WarehouseService(
        ICatalogQueryService catalogQueryService,
        AdminHiitopDbContext context,
        IShopifyProductService shopifyProductService,
        IOptions<PosOptions> posOptions)
    {
        _catalogQueryService = catalogQueryService;
        _context = context;
        _shopifyProductService = shopifyProductService;
        _posOptions = posOptions.Value;
    }

    public async Task<object> GetAsync(int? perPage, int page, string? search, bool includeShopify = false)
    {
        if (!includeShopify)
        {
            if (perPage.HasValue)
            {
                IQueryable<Warehouse> query = _context.Warehouses.AsNoTracking()
                    .Include(item => item.WarehouseType)
                    .Include(item => item.Province)
                    .Include(item => item.District)
                    .OrderBy(item => item.Name);

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(item =>
                        item.Name.Contains(search) ||
                        item.Code.Contains(search) ||
                        (item.City != null && item.City.Contains(search)));
                }

                return await PaginationHelper.CreateAsync(query, page, perPage.Value);
            }

            return (object)await _catalogQueryService.GetWarehousesAsync();
        }

        List<WarehouseListRow> localRows = await _context.Warehouses
            .AsNoTracking()
            .Include(item => item.WarehouseType)
            .Include(item => item.Province)
            .Include(item => item.District)
            .OrderBy(item => item.Name)
            .Select(item => new WarehouseListRow
            {
                Id = item.Id,
                Name = item.Name,
                Code = item.Code,
                Address = null,
                City = item.City,
                IsActive = item.IsActive,
                IsPos = item.IsPos,
                WarehouseTypeId = item.WarehouseTypeId,
                ProvinceId = item.ProvinceId,
                DistrictId = item.DistrictId,
                WarehouseType = item.WarehouseType == null ? null : new
                {
                    item.WarehouseType.Id,
                    item.WarehouseType.Name,
                    item.WarehouseType.Code
                },
                Province = item.Province == null ? null : new
                {
                    item.Province.Id,
                    item.Province.Name,
                    item.Province.Code
                },
                District = item.District == null ? null : new
                {
                    item.District.Id,
                    item.District.Name,
                    item.District.Code
                },
                Source = "mysql"
            })
            .ToListAsync();

        // Shopify rows come from the local DB table (populated via sync).
        // Fall back to live Shopify API if never synced yet (uneditable rows get negative IDs).
        List<WarehouseListRow> shopifyRows = await BuildShopifyRowsAsync();

        IEnumerable<WarehouseListRow> combinedRows = localRows.Concat(shopifyRows);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            combinedRows = combinedRows.Where(item =>
                item.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(item.Address) && item.Address.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(item.City) && item.City.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        List<WarehouseListRow> orderedRows = combinedRows
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Source, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!perPage.HasValue)
        {
            return orderedRows;
        }

        return ToPagedResponse(orderedRows, page, perPage.Value);
    }

    public Task<Warehouse?> GetByIdAsync(int id)
        => _context.Warehouses.AsNoTracking()
            .Include(item => item.WarehouseType)
            .Include(item => item.Province)
            .Include(item => item.District)
            .FirstOrDefaultAsync(item => item.Id == id);

    public async Task<Warehouse> CreateAsync(Warehouse request)
    {
        if (request.IsPos)
        {
            await ValidatePosLimitAsync(excludedId: null);
        }

        _context.Warehouses.Add(request);
        await _context.SaveChangesAsync();
        await _context.Entry(request).Reference(w => w.Province).LoadAsync();
        await _context.Entry(request).Reference(w => w.District).LoadAsync();
        return request;
    }

    public async Task<Warehouse> UpdateAsync(int id, Warehouse request)
    {
        // Shopify location row (id >= ShopifyIdOffset)
        if (id >= ShopifyIdOffset)
        {
            return await UpdateShopifyLocationAsync(id - ShopifyIdOffset, request);
        }

        // Local warehouse
        Warehouse entity = await FindAsync(id);

        if (request.IsPos && !entity.IsPos)
        {
            await ValidatePosLimitAsync(excludedId: id);
        }

        entity.Name = string.IsNullOrWhiteSpace(request.Name) ? entity.Name : request.Name;
        entity.Code = string.IsNullOrWhiteSpace(request.Code) ? entity.Code : request.Code;
        entity.City = request.City ?? entity.City;
        entity.Type = request.Type ?? entity.Type;
        entity.IsActive = request.IsActive;
        entity.IsPos = request.IsPos;
        entity.WarehouseTypeId = request.WarehouseTypeId;
        entity.ProvinceId = request.ProvinceId;
        entity.DistrictId = request.DistrictId;
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task DeleteAsync(int id)
    {
        if (id >= ShopifyIdOffset)
        {
            throw new AppException("Las ubicaciones de Shopify no se pueden eliminar. Usa el botón Sync para gestionar el ciclo de vida.", 422);
        }

        Warehouse entity = await FindAsync(id);
        _context.Warehouses.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task SyncShopifyLocationsAsync()
    {
        List<ShopifyLocationResponse> apiLocations = await _shopifyProductService.GetLocationsAsync();

        foreach (ShopifyLocationResponse loc in apiLocations)
        {
            ShopifyLocation? existing = await _context.ShopifyLocations
                .FirstOrDefaultAsync(s => s.ShopifyLocationId == loc.Id);

            if (existing is null)
            {
                _context.ShopifyLocations.Add(new ShopifyLocation
                {
                    ShopifyLocationId = loc.Id,
                    Name = loc.Name,
                    IsActive = loc.Active,
                    IsPos = false,
                    Address = loc.Address,
                    City = loc.City,
                    SyncedAt = DateTimeOffset.UtcNow
                });
            }
            else
            {
                // Preserve IsPos — only the user controls that flag
                existing.Name = loc.Name;
                existing.IsActive = loc.Active;
                existing.Address = loc.Address;
                existing.City = loc.City;
                existing.SyncedAt = DateTimeOffset.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<List<WarehouseListRow>> BuildShopifyRowsAsync()
    {
        List<ShopifyLocation> dbLocations = await _context.ShopifyLocations
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync();

        if (dbLocations.Count > 0)
        {
            return dbLocations.Select(sl => new WarehouseListRow
            {
                Id = ShopifyIdOffset + sl.Id,
                Name = sl.Name,
                Code = $"SHOPIFY-{sl.ShopifyLocationId}",
                Address = sl.Address,
                City = sl.City,
                IsActive = sl.IsActive,
                IsPos = sl.IsPos,
                Source = "shopify",
                ShopifyLocationId = sl.ShopifyLocationId
            }).ToList();
        }

        // Fallback: never synced → show live Shopify rows (read-only; negative IDs signal "unsynced")
        return (await _shopifyProductService.GetLocationsAsync())
            .Select((location, index) => new WarehouseListRow
            {
                Id = -100_000 - index,
                Name = location.Name,
                Code = $"SHOPIFY-{location.Id}",
                Address = location.Address,
                City = location.City,
                IsActive = location.Active,
                IsPos = false,
                Source = "shopify",
                ShopifyLocationId = location.Id
            })
            .ToList();
    }

    private async Task<Warehouse> UpdateShopifyLocationAsync(int localId, Warehouse request)
    {
        ShopifyLocation sl = await _context.ShopifyLocations
            .FirstOrDefaultAsync(s => s.Id == localId)
            ?? throw new AppException("Ubicación de Shopify no encontrada.", 404);

        sl.IsPos = request.IsPos;
        sl.IsActive = request.IsActive;
        await _context.SaveChangesAsync();

        // Return a transient Warehouse to satisfy the interface return type
        return new Warehouse
        {
            Id = ShopifyIdOffset + sl.Id,
            Name = sl.Name,
            Code = $"SHOPIFY-{sl.ShopifyLocationId}",
            City = sl.City,
            IsActive = sl.IsActive,
            IsPos = sl.IsPos
        };
    }

    private async Task ValidatePosLimitAsync(int? excludedId)
    {
        IQueryable<Warehouse> query = _context.Warehouses.Where(w => w.IsPos);
        if (excludedId.HasValue)
        {
            query = query.Where(w => w.Id != excludedId.Value);
        }

        int current = await query.CountAsync();
        if (current >= _posOptions.MaxPosWarehouses)
        {
            throw new AppException(
                $"Se alcanzó el límite de {_posOptions.MaxPosWarehouses} punto(s) de venta permitidos. " +
                "Ajusta 'Pos:MaxPosWarehouses' en appsettings.json para aumentar el límite.", 422);
        }
    }

    private async Task<Warehouse> FindAsync(int id)
    {
        Warehouse? entity = await _context.Warehouses.FirstOrDefaultAsync(item => item.Id == id);
        if (entity is null) throw new AppException("Almacén no encontrado.", 404);
        return entity;
    }

    private static PagedResponse<WarehouseListRow> ToPagedResponse(IReadOnlyList<WarehouseListRow> items, int page, int perPage)
    {
        int safePage = page < 1 ? 1 : page;
        int safePerPage = perPage < 1 ? 15 : perPage;
        int total = items.Count;
        int lastPage = total == 0 ? 1 : (int)Math.Ceiling(total / (double)safePerPage);

        List<WarehouseListRow> data = items
            .Skip((safePage - 1) * safePerPage)
            .Take(safePerPage)
            .ToList();

        return new PagedResponse<WarehouseListRow>
        {
            Data = data,
            CurrentPage = safePage,
            LastPage = lastPage,
            PerPage = safePerPage,
            Total = total
        };
    }
}

internal sealed class WarehouseListRow
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string? Address { get; init; }
    public string? City { get; init; }
    public bool IsActive { get; init; }
    public bool IsPos { get; init; }
    public int? WarehouseTypeId { get; init; }
    public int? ProvinceId { get; init; }
    public int? DistrictId { get; init; }
    public object? WarehouseType { get; init; }
    public object? Province { get; init; }
    public object? District { get; init; }
    public string Source { get; init; } = "mysql";
    public long? ShopifyLocationId { get; init; }
}
