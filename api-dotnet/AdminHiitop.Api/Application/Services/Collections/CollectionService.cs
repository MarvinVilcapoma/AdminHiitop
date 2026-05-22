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

    public async Task<object> GetAsync(int perPage, int page, string? search)
    {
        IQueryable<Collection> query = _context.Collections.AsNoTracking().OrderBy(item => item.Name);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(item => item.Name.Contains(search) || (item.Description != null && item.Description.Contains(search)));
        return await PaginationHelper.CreateAsync(query, page, perPage);
    }

    public Task<Collection?> GetByIdAsync(int id)
        => _context.Collections.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);

    public async Task<Collection> CreateAsync(Collection request)
    {
        var entity = new Collection { Name = request.Name, Description = request.Description, IsActive = request.IsActive };
        _context.Collections.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<Collection> UpdateAsync(int id, Collection request)
    {
        Collection entity = await FindAsync(id);
        if (!string.IsNullOrWhiteSpace(request.Name)) entity.Name = request.Name;
        entity.Description = request.Description ?? entity.Description;
        entity.IsActive = request.IsActive;
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task DeleteAsync(int id)
    {
        Collection entity = await FindAsync(id);
        _context.Collections.Remove(entity);
        await _context.SaveChangesAsync();
    }

    private async Task<Collection> FindAsync(int id)
    {
        Collection? entity = await _context.Collections.FirstOrDefaultAsync(item => item.Id == id);
        if (entity is null) throw new AppException("Colección no encontrada.", 404);
        return entity;
    }
}
