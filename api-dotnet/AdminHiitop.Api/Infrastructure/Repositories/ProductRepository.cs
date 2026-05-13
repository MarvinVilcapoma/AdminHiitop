using AdminHiitop.Api.Application.DTOs.Products;
using AdminHiitop.Api.Application.Interfaces.Repositories;
using AdminHiitop.Api.Domain.Inventory.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Helpers;
using AdminHiitop.Api.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Infrastructure.Repositories;

public sealed class ProductRepository : IProductRepository
{
    private readonly AdminHiitopDbContext _context;

    public ProductRepository(AdminHiitopDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResponse<ProductPagedItemResponse>> GetPagedAsync(ProductQueryRequest request)
    {
        IQueryable<Product> query = _context.Products
            .AsNoTracking()
            .Include(item => item.ProductType)
            .Include(item => item.Collection)
            .Include(item => item.ProductColors)
            .ThenInclude(item => item.Color)
            .Include(item => item.Stocks)
            .OrderBy(item => item.Name);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string term = request.Search.Trim();
            query = query.Where(item => item.Name.Contains(term) || (item.Sku != null && item.Sku.Contains(term)));
        }

        IQueryable<ProductPagedItemResponse> shapedQuery = query.Select(item => new ProductPagedItemResponse
        {
            Id = item.Id,
            Name = item.Name,
            Sku = item.Sku,
            Description = item.Description,
            BasePrice = item.BasePrice,
            UnitCost = item.UnitCost,
            IsActive = item.IsActive,
            ProductTypeId = item.ProductTypeId,
            CollectionId = item.CollectionId,
            ProductType = item.ProductType == null
                ? null
                : new ProductCatalogReferenceResponse
                {
                    Id = item.ProductType.Id,
                    Name = item.ProductType.Name
                },
            Collection = item.Collection == null
                ? null
                : new ProductCatalogReferenceResponse
                {
                    Id = item.Collection.Id,
                    Name = item.Collection.Name
                },
            Colors = item.ProductColors
                .Where(colorLink => colorLink.Color != null)
                .Select(colorLink => new ProductColorResponse
                {
                    Id = colorLink.Color!.Id,
                    Name = colorLink.Color.Name,
                    HexCode = colorLink.Color.HexCode
                })
                .ToList(),
            TotalStock = item.Stocks.Sum(stock => stock.Quantity)
        });

        return await PaginationHelper.CreateAsync(shapedQuery, request.Page, request.PerPage ?? 15);
    }

    public async Task<IReadOnlyList<ProductListItemResponse>> GetListAsync(string? search)
    {
        IQueryable<Product> query = _context.Products
            .AsNoTracking()
            .Include(item => item.ProductType)
            .Include(item => item.Collection)
            .Include(item => item.Stocks);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(item => item.Name.Contains(term) || (item.Sku != null && item.Sku.Contains(term)));
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

    public Task<Product?> GetByIdAsync(int id)
    {
        return _context.Products.FirstOrDefaultAsync(item => item.Id == id);
    }

    public async Task<ProductDetailResponse?> GetDetailByIdAsync(int id)
    {
        Product? product = await _context.Products
            .AsNoTracking()
            .Include(item => item.ProductType)
            .Include(item => item.Collection)
            .Include(item => item.ProductColors)
            .ThenInclude(item => item.Color)
            .Include(item => item.Stocks)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (product is null)
        {
            return null;
        }

        return Application.Helpers.InventoryMappingHelper.MapProductDetail(product);
    }

    public Task<bool> ExistsBySkuAsync(string? sku, int? excludedProductId = null)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return Task.FromResult(false);
        }

        string normalizedSku = sku.Trim();

        return _context.Products.AnyAsync(
            item => item.Sku == normalizedSku && (!excludedProductId.HasValue || item.Id != excludedProductId.Value));
    }

    public Task AddAsync(Product product)
    {
        return _context.Products.AddAsync(product).AsTask();
    }

    public Task DeleteAsync(Product product)
    {
        _context.Products.Remove(product);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}
