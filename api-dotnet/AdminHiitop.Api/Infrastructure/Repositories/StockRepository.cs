using AdminHiitop.Api.Application.DTOs.Stocks;
using AdminHiitop.Api.Application.Helpers;
using AdminHiitop.Api.Application.Interfaces.Repositories;
using AdminHiitop.Api.Domain.Inventory.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Helpers;
using AdminHiitop.Api.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Infrastructure.Repositories;

public sealed class StockRepository : IStockRepository
{
    private readonly AdminHiitopDbContext _context;

    public StockRepository(AdminHiitopDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResponse<StockResponse>> GetPagedAsync(StockQueryRequest request)
    {
        await ConsolidateDuplicatesAsync();
        IQueryable<Stock> query = BuildBaseQuery();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string term = request.Search.Trim();
            query = query.Where(item => item.Product.Name.Contains(term) || (item.Product.Sku != null && item.Product.Sku.Contains(term)));
        }

        if (request.WarehouseId.HasValue)
        {
            query = query.Where(item => item.WarehouseId == request.WarehouseId.Value);
        }

        if (request.ColorId.HasValue)
        {
            query = query.Where(item => item.ColorId == request.ColorId.Value);
        }

        if (request.ProductTypeId.HasValue)
        {
            query = query.Where(item => item.Product.ProductTypeId == request.ProductTypeId.Value);
        }

        if (request.CollectionId.HasValue)
        {
            query = query.Where(item => item.Product.CollectionId == request.CollectionId.Value);
        }

        if (request.LowStock)
        {
            query = query.Where(item => item.Quantity <= 5);
        }

        IQueryable<StockResponse> shapedQuery = query.Select(item => new StockResponse
        {
            Id = item.Id,
            ProductId = item.ProductId,
            WarehouseId = item.WarehouseId,
            ColorId = item.ColorId,
            Size = item.Size,
            Quantity = item.Quantity,
            Reserved = item.Reserved,
            Available = item.Quantity - item.Reserved,
            Product = new StockProductReferenceResponse
            {
                Id = item.Product.Id,
                Name = item.Product.Name,
                Sku = item.Product.Sku,
                ProductType = item.Product.ProductType == null
                    ? null
                    : new StockCatalogReferenceResponse
                    {
                        Id = item.Product.ProductType.Id,
                        Name = item.Product.ProductType.Name
                    },
                Collection = item.Product.Collection == null
                    ? null
                    : new StockCatalogReferenceResponse
                    {
                        Id = item.Product.Collection.Id,
                        Name = item.Product.Collection.Name
                    }
            },
            Warehouse = new StockWarehouseReferenceResponse
            {
                Id = item.Warehouse.Id,
                Name = item.Warehouse.Name,
                Type = item.Warehouse.Type
            },
            Color = item.Color == null
                ? null
                : new StockColorReferenceResponse
                {
                    Id = item.Color.Id,
                    Name = item.Color.Name,
                    HexCode = item.Color.HexCode
                }
        });

        return await PaginationHelper.CreateAsync(shapedQuery, request.Page, request.PerPage);
    }

    public async Task<IReadOnlyList<StockSummaryResponse>> GetSummaryAsync()
    {
        await ConsolidateDuplicatesAsync();
        return await _context.Stocks
            .AsNoTracking()
            .GroupBy(item => new { item.WarehouseId, item.Warehouse.Name, item.Warehouse.Type })
            .Select(group => new StockSummaryResponse
            {
                WarehouseId = group.Key.WarehouseId,
                WarehouseName = group.Key.Name,
                WarehouseType = group.Key.Type,
                TotalQuantity = group.Sum(item => item.Quantity),
                TotalItems = group.Count()
            })
            .ToListAsync();
    }

    public async Task<object> GetAvailableGroupedAsync(int? productId, int? warehouseId)
    {
        await ConsolidateDuplicatesAsync();
        IQueryable<Stock> query = BuildBaseQuery()
            .Where(item => item.Quantity - item.Reserved > 0);

        if (productId.HasValue)
            query = query.Where(item => item.ProductId == productId.Value);

        if (warehouseId.HasValue)
            query = query.Where(item => item.WarehouseId == warehouseId.Value);

        var rows = await query
            .Select(item => new
            {
                ColorId = item.ColorId,
                ColorName = item.Color != null ? item.Color.Name : (string?)null,
                ColorHex  = item.Color != null ? item.Color.HexCode : (string?)null,
                Size      = item.Size,
                Available = item.Quantity - item.Reserved,
            })
            .ToListAsync();

        var byColor = rows
            .GroupBy(r => r.ColorId)
            .Select(g => new
            {
                color_id         = g.Key,
                color            = g.First().ColorName is null ? null : new
                {
                    id       = g.Key,
                    name     = g.First().ColorName,
                    hex_code = g.First().ColorHex,
                },
                sizes            = g.Where(r => !string.IsNullOrWhiteSpace(r.Size))
                                    .Select(r => r.Size!)
                                    .Distinct()
                                    .OrderBy(s => s)
                                    .ToList(),
                total_available  = g.Sum(r => r.Available),
            })
            .OrderBy(g => g.color?.name ?? "")
            .ToList();

        return new { by_color = byColor };
    }

    public async Task<IReadOnlyList<StockLookupResponse>> GetLookupAsync(
        string? search, int? warehouseId, int? colorId, bool availableOnly, int limit)
    {
        await ConsolidateDuplicatesAsync();
        IQueryable<Stock> query = BuildBaseQuery();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(item =>
                item.Product.Name.Contains(term) ||
                (item.Product.Sku != null && item.Product.Sku.Contains(term)) ||
                (item.Color != null && item.Color.Name.Contains(term)) ||
                (item.Size != null && item.Size.Contains(term)));
        }

        if (warehouseId.HasValue)
            query = query.Where(item => item.WarehouseId == warehouseId.Value);

        if (colorId.HasValue)
            query = query.Where(item => item.ColorId == colorId.Value);

        if (availableOnly)
            query = query.Where(item => item.Quantity - item.Reserved > 0);

        return await query
            .Take(limit)
            .Select(item => new StockLookupResponse
            {
                StockId      = item.Id,
                ProductId    = item.ProductId,
                ProductName  = item.Product.Name,
                Sku          = item.Product.Sku,
                WarehouseId  = item.WarehouseId,
                WarehouseName = item.Warehouse.Name,
                ColorId      = item.ColorId,
                ColorName    = item.Color != null ? item.Color.Name : null,
                Size         = item.Size,
                AvailableQty = item.Quantity - item.Reserved,
                UnitPrice    = item.Product.BasePrice,
                UnitCost     = item.Product.UnitCost,
                VariantLabel = ((item.Color != null ? item.Color.Name : string.Empty) + " " + (item.Size ?? string.Empty)).Trim()
            })
            .ToListAsync();
    }

    public Task<Stock?> GetByIdAsync(int id)
    {
        return _context.Stocks
            .Include(item => item.Product)
            .ThenInclude(item => item.ProductType)
            .Include(item => item.Product)
            .ThenInclude(item => item.Collection)
            .Include(item => item.Warehouse)
            .Include(item => item.Color)
            .FirstOrDefaultAsync(item => item.Id == id);
    }

    public async Task<StockResponse?> GetDetailByIdAsync(int id)
    {
        await ConsolidateDuplicatesAsync();
        Stock? stock = await GetByIdAsync(id);
        return stock is null ? null : InventoryMappingHelper.MapStock(stock);
    }

    public async Task ConsolidateDuplicatesAsync()
    {
        List<Stock> stocks = await _context.Stocks
            .OrderBy(item => item.Id)
            .ToListAsync();

        foreach (var group in stocks.GroupBy(item => new
                 {
                     item.ProductId,
                     item.WarehouseId,
                     item.ColorId,
                     item.Size
                 }))
        {
            Stock primary = group.First();
            List<Stock> duplicates = group.Skip(1).ToList();

            if (duplicates.Count == 0)
            {
                continue;
            }

            foreach (Stock duplicate in duplicates)
            {
                primary.Quantity += duplicate.Quantity;
                primary.Reserved += duplicate.Reserved;
                _context.Stocks.Remove(duplicate);
            }
        }

        if (_context.ChangeTracker.HasChanges())
        {
            await _context.SaveChangesAsync();
        }
    }

    public Task<Stock?> FindTransferTargetAsync(int productId, int warehouseId, int? colorId, string? size)
    {
        return _context.Stocks.FirstOrDefaultAsync(
            item => item.ProductId == productId &&
                    item.WarehouseId == warehouseId &&
                    item.ColorId == colorId &&
                    item.Size == size);
    }

    public Task<List<Stock>> FindMatchingStocksAsync(int productId, int warehouseId, int? colorId, string? size)
    {
        return _context.Stocks
            .Where(item => item.ProductId == productId &&
                           item.WarehouseId == warehouseId &&
                           item.ColorId == colorId &&
                           item.Size == size)
            .OrderBy(item => item.Id)
            .ToListAsync();
    }

    public Task AddAsync(Stock stock)
    {
        return _context.Stocks.AddAsync(stock).AsTask();
    }

    public Task AddRangeAsync(IReadOnlyCollection<Stock> stocks)
    {
        return _context.Stocks.AddRangeAsync(stocks);
    }

    public Task DeleteAsync(Stock stock)
    {
        _context.Stocks.Remove(stock);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }

    private IQueryable<Stock> BuildBaseQuery()
    {
        return _context.Stocks
            .AsNoTracking()
            .Where(item => item.Product.IsActive)
            .Include(item => item.Product)
            .ThenInclude(item => item.ProductType)
            .Include(item => item.Product)
            .ThenInclude(item => item.Collection)
            .Include(item => item.Warehouse)
            .Include(item => item.Color)
            .OrderBy(item => item.Product.Name);
    }
}
