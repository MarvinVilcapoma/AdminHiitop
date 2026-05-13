using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.PurchaseTypes;

public sealed class PurchaseTypeService : IPurchaseTypeService
{
    private readonly AdminHiitopDbContext _context;

    public PurchaseTypeService(AdminHiitopDbContext context) => _context = context;

    public async Task<object> GetAsync(int perPage, int page, string? search, CancellationToken cancellationToken)
    {
        IQueryable<PurchaseType> query = _context.PurchaseTypes.AsNoTracking().OrderBy(item => item.Name);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(item => item.Name.Contains(search) || item.Slug.Contains(search));
        return await PaginationHelper.CreateAsync(query, page, perPage, cancellationToken);
    }

    public Task<PurchaseType?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => _context.PurchaseTypes.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<PurchaseType> CreateAsync(PurchaseType request, CancellationToken cancellationToken)
    {
        request.Slug = string.IsNullOrWhiteSpace(request.Slug) ? request.Name.ToLower().Replace(' ', '-') : request.Slug;
        _context.PurchaseTypes.Add(request);
        await _context.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<PurchaseType> UpdateAsync(int id, PurchaseType request, CancellationToken cancellationToken)
    {
        PurchaseType entity = await FindAsync(id, cancellationToken);
        entity.Name = string.IsNullOrWhiteSpace(request.Name) ? entity.Name : request.Name;
        entity.Slug = string.IsNullOrWhiteSpace(request.Slug) ? entity.Slug : request.Slug;
        entity.IsActive = request.IsActive;
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        PurchaseType entity = await FindAsync(id, cancellationToken);
        _context.PurchaseTypes.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<PurchaseType> FindAsync(int id, CancellationToken cancellationToken)
    {
        PurchaseType? entity = await _context.PurchaseTypes.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) throw new AppException("Tipo de compra no encontrado.", 404);
        return entity;
    }
}
