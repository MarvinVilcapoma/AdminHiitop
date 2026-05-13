using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Collections;

public sealed class CollectionService : ICollectionService
{
    private readonly AdminHiitopDbContext _context;

    public CollectionService(AdminHiitopDbContext context) => _context = context;

    public async Task<object> GetAsync(int perPage, int page, string? search, CancellationToken cancellationToken)
    {
        IQueryable<Collection> query = _context.Collections.AsNoTracking().OrderBy(item => item.Name);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(item => item.Name.Contains(search) || (item.Description != null && item.Description.Contains(search)));
        return await PaginationHelper.CreateAsync(query, page, perPage, cancellationToken);
    }

    public Task<Collection?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => _context.Collections.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<Collection> CreateAsync(Collection request, CancellationToken cancellationToken)
    {
        var entity = new Collection { Name = request.Name, Description = request.Description, IsActive = request.IsActive };
        _context.Collections.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<Collection> UpdateAsync(int id, Collection request, CancellationToken cancellationToken)
    {
        Collection entity = await FindAsync(id, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.Name)) entity.Name = request.Name;
        entity.Description = request.Description ?? entity.Description;
        entity.IsActive = request.IsActive;
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        Collection entity = await FindAsync(id, cancellationToken);
        _context.Collections.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Collection> FindAsync(int id, CancellationToken cancellationToken)
    {
        Collection? entity = await _context.Collections.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) throw new AppException("Colección no encontrada.", 404);
        return entity;
    }
}
