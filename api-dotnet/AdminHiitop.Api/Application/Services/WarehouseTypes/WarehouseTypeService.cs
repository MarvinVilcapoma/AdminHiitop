using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.WarehouseTypes;

public sealed class WarehouseTypeService : IWarehouseTypeService
{
    private readonly AdminHiitopDbContext _context;

    public WarehouseTypeService(AdminHiitopDbContext context) => _context = context;

    public async Task<object> GetAsync(int perPage, int page, string? search)
    {
        IQueryable<WarehouseType> query = _context.WarehouseTypes.AsNoTracking().OrderBy(item => item.Name);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(item => item.Name.Contains(search) || item.Code.Contains(search));
        return await PaginationHelper.CreateAsync(query, page, perPage);
    }

    public Task<WarehouseType?> GetByIdAsync(int id)
        => _context.WarehouseTypes.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);

    public async Task<WarehouseType> CreateAsync(WarehouseType request)
    {
        _context.WarehouseTypes.Add(request);
        await _context.SaveChangesAsync();
        return request;
    }

    public async Task<WarehouseType> UpdateAsync(int id, WarehouseType request)
    {
        WarehouseType entity = await FindAsync(id);
        entity.Name = string.IsNullOrWhiteSpace(request.Name) ? entity.Name : request.Name;
        entity.Code = string.IsNullOrWhiteSpace(request.Code) ? entity.Code : request.Code;
        entity.IsActive = request.IsActive;
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task DeleteAsync(int id)
    {
        WarehouseType entity = await FindAsync(id);
        _context.WarehouseTypes.Remove(entity);
        await _context.SaveChangesAsync();
    }

    private async Task<WarehouseType> FindAsync(int id)
    {
        WarehouseType? entity = await _context.WarehouseTypes.FirstOrDefaultAsync(item => item.Id == id);
        if (entity is null) throw new AppException("Tipo de almacén no encontrado.", 404);
        return entity;
    }
}
