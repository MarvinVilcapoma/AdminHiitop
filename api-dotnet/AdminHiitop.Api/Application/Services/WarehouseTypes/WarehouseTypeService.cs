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

    public async Task<object> GetAsync(int perPage, int page, string? search, CancellationToken cancellationToken)
    {
        IQueryable<WarehouseType> query = _context.WarehouseTypes.AsNoTracking().OrderBy(item => item.Name);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(item => item.Name.Contains(search) || item.Code.Contains(search));
        return await PaginationHelper.CreateAsync(query, page, perPage, cancellationToken);
    }

    public Task<WarehouseType?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => _context.WarehouseTypes.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<WarehouseType> CreateAsync(WarehouseType request, CancellationToken cancellationToken)
    {
        _context.WarehouseTypes.Add(request);
        await _context.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<WarehouseType> UpdateAsync(int id, WarehouseType request, CancellationToken cancellationToken)
    {
        WarehouseType entity = await FindAsync(id, cancellationToken);
        entity.Name = string.IsNullOrWhiteSpace(request.Name) ? entity.Name : request.Name;
        entity.Code = string.IsNullOrWhiteSpace(request.Code) ? entity.Code : request.Code;
        entity.IsActive = request.IsActive;
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        WarehouseType entity = await FindAsync(id, cancellationToken);
        _context.WarehouseTypes.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<WarehouseType> FindAsync(int id, CancellationToken cancellationToken)
    {
        WarehouseType? entity = await _context.WarehouseTypes.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) throw new AppException("Tipo de almacén no encontrado.", 404);
        return entity;
    }
}
