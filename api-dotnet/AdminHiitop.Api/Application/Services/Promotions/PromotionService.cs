using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Promotions;

public sealed class PromotionService : IPromotionService
{
    private readonly AdminHiitopDbContext _context;

    public PromotionService(AdminHiitopDbContext context) => _context = context;

    public async Task<object> GetAsync(int perPage, int page, string? search, bool activeOnly, bool inactiveOnly, CancellationToken cancellationToken)
    {
        var query = _context.Promotions.AsNoTracking()
            .Include(item => item.Items).ThenInclude(item => item.ProductType)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(item => item.Name.Contains(search) || (item.Description != null && item.Description.Contains(search)));

        if (activeOnly)
            query = query.Where(item => item.IsActive);
        else if (inactiveOnly)
            query = query.Where(item => !item.IsActive);

        return await PaginationHelper.CreateAsync(query.OrderByDescending(item => item.Id), page, perPage, cancellationToken);
    }

    public Task<Promotion?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => _context.Promotions.AsNoTracking().Include(item => item.Items).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<Promotion> CreateAsync(Promotion request, CancellationToken cancellationToken)
    {
        _context.Promotions.Add(request);
        await _context.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<Promotion> UpdateAsync(int id, Promotion request, CancellationToken cancellationToken)
    {
        Promotion entity = await FindAsync(id, cancellationToken);
        entity.Name = string.IsNullOrWhiteSpace(request.Name) ? entity.Name : request.Name;
        entity.Description = request.Description ?? entity.Description;
        entity.IsActive = request.IsActive;
        entity.FixedPrice = request.FixedPrice;
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        Promotion entity = await FindAsync(id, cancellationToken);
        _context.Promotions.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Promotion> FindAsync(int id, CancellationToken cancellationToken)
    {
        Promotion? entity = await _context.Promotions.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) throw new AppException("Promoción no encontrada.", 404);
        return entity;
    }
}
