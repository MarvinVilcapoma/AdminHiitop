using AdminHiitop.Api.Application.DTOs.Products;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Products;

public sealed class ProductQueryService : IProductQueryService
{
    private readonly AdminHiitopDbContext _context;

    public ProductQueryService(AdminHiitopDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ProductListItemResponse>> GetAsync(string? search)
    {
        string normalizedSearch = NormalizeSearch(search);
        IQueryable<Domain.Inventory.Entities.Product> query = _context.Products
            .AsNoTracking()
            .Include(item => item.ProductType)
            .Include(item => item.Collection)
            .Include(item => item.Stocks);

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(item =>
                item.Name.Contains(normalizedSearch) ||
                (item.Sku != null && item.Sku.Contains(normalizedSearch)));
        }

        return await query
            .OrderBy(item => item.Name)
            .Select(item => new ProductListItemResponse
            {
                Id = item.Id,
                Name = item.Name,
                Sku = item.Sku,
                ProductTypeName = item.ProductType != null ? item.ProductType.Name : null,
                CollectionName = item.Collection != null ? item.Collection.Name : null,
                BasePrice = item.BasePrice,
                UnitCost = item.UnitCost,
                IsActive = item.IsActive,
                TotalStock = item.Stocks.Sum(stock => stock.Quantity)
            })
            .ToListAsync();
    }

    private static string NormalizeSearch(string? search) =>
        string.IsNullOrWhiteSpace(search) ? string.Empty : search.Trim();
}
