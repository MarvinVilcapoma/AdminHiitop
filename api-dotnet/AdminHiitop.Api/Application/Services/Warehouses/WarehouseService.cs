using AdminHiitop.Api.Application.DTOs.Common;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.Extensions.Options;

namespace AdminHiitop.Api.Application.Services.Warehouses;

public sealed class WarehouseService : IWarehouseService
{
    private readonly ICatalogQueryService _catalogQueryService;
    private readonly AdminHiitopDbContext _context;
    private readonly PosOptions _posOptions;

    public WarehouseService(
        ICatalogQueryService catalogQueryService,
        AdminHiitopDbContext context,
        IOptions<PosOptions> posOptions)
    {
        _catalogQueryService = catalogQueryService;
        _context = context;
        _posOptions = posOptions.Value;
    }

    public async Task<object> GetAsync(int? perPage, int page, string? search, CancellationToken cancellationToken)
    {
        if (perPage.HasValue)
        {
            IQueryable<Warehouse> query = _context.Warehouses.AsNoTracking()
                .Include(item => item.WarehouseType)
                .Include(item => item.Province)
                .Include(item => item.District)
                .OrderBy(item => item.Name);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(item =>
                    item.Name.Contains(search) ||
                    item.Code.Contains(search) ||
                    (item.City != null && item.City.Contains(search)));

            return await PaginationHelper.CreateAsync(query, page, perPage.Value, cancellationToken);
        }
        return (object)await _catalogQueryService.GetWarehousesAsync();
    }

    public Task<Warehouse?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => _context.Warehouses.AsNoTracking()
            .Include(item => item.WarehouseType)
            .Include(item => item.Province)
            .Include(item => item.District)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<Warehouse> CreateAsync(Warehouse request, CancellationToken cancellationToken)
    {
        if (request.IsPos)
            await ValidatePosLimitAsync(excludedId: null, cancellationToken);

        _context.Warehouses.Add(request);
        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(request).Reference(w => w.Province).LoadAsync(cancellationToken);
        await _context.Entry(request).Reference(w => w.District).LoadAsync(cancellationToken);
        return request;
    }

    public async Task<Warehouse> UpdateAsync(int id, Warehouse request, CancellationToken cancellationToken)
    {
        Warehouse entity = await FindAsync(id, cancellationToken);

        if (request.IsPos && !entity.IsPos)
            await ValidatePosLimitAsync(excludedId: id, cancellationToken);

        entity.Name          = string.IsNullOrWhiteSpace(request.Name) ? entity.Name : request.Name;
        entity.Code          = string.IsNullOrWhiteSpace(request.Code) ? entity.Code : request.Code;
        entity.City          = request.City ?? entity.City;
        entity.Type          = request.Type ?? entity.Type;
        entity.IsActive      = request.IsActive;
        entity.IsPos         = request.IsPos;
        entity.WarehouseTypeId = request.WarehouseTypeId;
        entity.ProvinceId    = request.ProvinceId;
        entity.DistrictId    = request.DistrictId;
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        Warehouse entity = await FindAsync(id, cancellationToken);
        _context.Warehouses.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidatePosLimitAsync(int? excludedId, CancellationToken cancellationToken)
    {
        IQueryable<Warehouse> query = _context.Warehouses.Where(w => w.IsPos);
        if (excludedId.HasValue)
            query = query.Where(w => w.Id != excludedId.Value);

        int current = await query.CountAsync(cancellationToken);
        if (current >= _posOptions.MaxPosWarehouses)
            throw new AppException(
                $"Se alcanzó el límite de {_posOptions.MaxPosWarehouses} punto(s) de venta permitidos. " +
                "Ajusta 'Pos:MaxPosWarehouses' en appsettings.json para aumentar el límite.", 422);
    }

    private async Task<Warehouse> FindAsync(int id, CancellationToken cancellationToken)
    {
        Warehouse? entity = await _context.Warehouses.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) throw new AppException("Almacén no encontrado.", 404);
        return entity;
    }
}
