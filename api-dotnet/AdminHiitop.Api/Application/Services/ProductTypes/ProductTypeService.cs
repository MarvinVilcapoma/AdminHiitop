using AdminHiitop.Api.Application.DTOs.ProductTypes;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.ProductTypes;

public sealed class ProductTypeService : IProductTypeService
{
    private readonly AdminHiitopDbContext _context;
    private readonly IShopifyProductService _shopifyProductService;

    public ProductTypeService(AdminHiitopDbContext context, IShopifyProductService shopifyProductService)
    {
        _context = context;
        _shopifyProductService = shopifyProductService;
    }

    public async Task<object> GetAsync(int perPage, int page, string? search, bool includeShopify = false)
    {
        IQueryable<ProductType> baseQuery = _context.ProductTypes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            baseQuery = baseQuery.Where(item => item.Name.Contains(search) || item.Slug.Contains(search));
        }

        List<ProductTypeListRow> localRows = await baseQuery
            .OrderBy(item => item.Name)
            .Include(item => item.Sizes)
            .Select(item => new ProductTypeListRow
            {
                Id = item.Id,
                Name = item.Name,
                Slug = item.Slug,
                IsActive = item.IsActive,
                Sizes = item.Sizes
                    .OrderBy(size => size.SortOrder)
                    .Select(size => new { size.Id, size.Name, size.SortOrder })
                    .ToList(),
                Source = "mysql"
            })
            .ToListAsync();

        if (!includeShopify)
        {
            return ToPagedResponse(localRows, page, perPage);
        }

        List<ProductTypeListRow> shopifyRows = (await _shopifyProductService.GetProductsAsync(search, 1, 250, "active"))
            .Products
            .Select(item => item.ProductType?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select((name, index) => new ProductTypeListRow
            {
                Id = -200000 - index,
                Name = name!,
                Slug = Slugify(name!),
                IsActive = true,
                Sizes = [],
                Source = "shopify"
            })
            .ToList();

        IEnumerable<ProductTypeListRow> combinedRows = localRows.Concat(shopifyRows);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            combinedRows = combinedRows.Where(item =>
                item.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.Slug.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        List<ProductTypeListRow> orderedRows = combinedRows
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Source, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return ToPagedResponse(orderedRows, page, perPage);
    }

    public Task<ProductType?> GetByIdAsync(int id)
        => _context.ProductTypes.AsNoTracking().Include(item => item.Sizes).FirstOrDefaultAsync(item => item.Id == id);

    public async Task<ProductType> CreateAsync(ProductType request)
    {
        request.Slug = string.IsNullOrWhiteSpace(request.Slug) ? Slugify(request.Name) : request.Slug;
        _context.ProductTypes.Add(request);
        await _context.SaveChangesAsync();
        return request;
    }

    public async Task<ProductType> UpdateAsync(int id, ProductType request)
    {
        ProductType entity = await FindAsync(id);
        entity.Name = string.IsNullOrWhiteSpace(request.Name) ? entity.Name : request.Name;
        entity.Slug = string.IsNullOrWhiteSpace(request.Slug) ? entity.Slug : request.Slug;
        entity.IsActive = request.IsActive;
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task DeleteAsync(int id)
    {
        ProductType entity = await FindAsync(id);
        _context.ProductTypes.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<object> SyncSizesAsync(int productTypeId, SyncSizesRequest request)
    {
        ProductType entity = await _context.ProductTypes.Include(item => item.Sizes)
            .FirstOrDefaultAsync(item => item.Id == productTypeId)
            ?? throw new AppException("Tipo de producto no encontrado.", 404);

        List<Size> currentSizes = entity.Sizes.ToList();
        entity.Sizes.Clear();
        await _context.SaveChangesAsync();

        if (currentSizes.Count > 0)
        {
            _context.Sizes.RemoveRange(currentSizes);
            await _context.SaveChangesAsync();
        }

        foreach (SizeRow row in request.Sizes ?? [])
        {
            entity.Sizes.Add(new Size { Name = row.Name, SortOrder = row.SortOrder });
        }

        await _context.SaveChangesAsync();

        List<object> sizes = entity.Sizes
            .OrderBy(item => item.SortOrder)
            .Select(item => new { item.Id, item.Name, item.SortOrder } as object)
            .ToList();

        return new { sizes };
    }

    private async Task<ProductType> FindAsync(int id)
    {
        ProductType? entity = await _context.ProductTypes.FirstOrDefaultAsync(item => item.Id == id);
        if (entity is null) throw new AppException("Tipo de producto no encontrado.", 404);
        return entity;
    }

    private static string Slugify(string value)
    {
        return value.Trim().ToLowerInvariant().Replace(' ', '-');
    }

    private static PagedResponse<ProductTypeListRow> ToPagedResponse(IReadOnlyList<ProductTypeListRow> items, int page, int perPage)
    {
        int safePage = page < 1 ? 1 : page;
        int safePerPage = perPage < 1 ? 15 : perPage;
        int total = items.Count;
        int lastPage = total == 0 ? 1 : (int)Math.Ceiling(total / (double)safePerPage);

        List<ProductTypeListRow> data = items
            .Skip((safePage - 1) * safePerPage)
            .Take(safePerPage)
            .ToList();

        return new PagedResponse<ProductTypeListRow>
        {
            Data = data,
            CurrentPage = safePage,
            LastPage = lastPage,
            PerPage = safePerPage,
            Total = total
        };
    }
}

internal sealed class ProductTypeListRow
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public IReadOnlyList<object> Sizes { get; init; } = [];
    public string Source { get; init; } = "mysql";
}
