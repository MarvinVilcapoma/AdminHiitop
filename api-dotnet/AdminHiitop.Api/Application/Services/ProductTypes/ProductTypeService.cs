using AdminHiitop.Api.Application.DTOs.ProductTypes;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.ProductTypes;

public sealed class ProductTypeService : IProductTypeService
{
    private readonly AdminHiitopDbContext _context;

    public ProductTypeService(AdminHiitopDbContext context) => _context = context;

    public async Task<object> GetAsync(int perPage, int page, string? search)
    {
        IQueryable<ProductType> baseQuery = _context.ProductTypes.AsNoTracking().OrderBy(item => item.Name);
        if (!string.IsNullOrWhiteSpace(search))
            baseQuery = baseQuery.Where(item => item.Name.Contains(search) || item.Slug.Contains(search));

        var query = baseQuery
            .Include(item => item.Sizes)
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.Slug,
                item.IsActive,
                sizes = item.Sizes.OrderBy(size => size.SortOrder).Select(size => new { size.Id, size.Name, size.SortOrder }).ToList()
            });

        return await PaginationHelper.CreateAsync(query, page, perPage);
    }

    public Task<ProductType?> GetByIdAsync(int id)
        => _context.ProductTypes.AsNoTracking().Include(item => item.Sizes).FirstOrDefaultAsync(item => item.Id == id);

    public async Task<ProductType> CreateAsync(ProductType request)
    {
        request.Slug = string.IsNullOrWhiteSpace(request.Slug) ? request.Name.ToLower().Replace(' ', '-') : request.Slug;
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
            entity.Sizes.Add(new Size { Name = row.Name, SortOrder = row.SortOrder });

        await _context.SaveChangesAsync();

        var sizes = entity.Sizes
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
}
