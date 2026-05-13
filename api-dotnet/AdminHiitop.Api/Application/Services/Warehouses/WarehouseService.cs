using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Warehouses;

public sealed class WarehouseService : IWarehouseService
{
    private readonly ICatalogQueryService _catalogQueryService;
    private readonly AdminHiitopDbContext _context;

    public WarehouseService(ICatalogQueryService catalogQueryService, AdminHiitopDbContext context)
    {
        _catalogQueryService = catalogQueryService;
        _context = context;
    }

    public async Task<object> GetAsync(int? perPage, int page, string? search, CancellationToken cancellationToken)
    {
        if (perPage.HasValue)
        {
            IQueryable<Warehouse> query = _context.Warehouses.AsNoTracking()
                .Include(item => item.WarehouseType)
                .Include(item => item.Province)
                .Include(item => item.District)
                .OrderBy(item => item.Name);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(item =>
                    item.Name.Contains(search) ||
                    item.Code.Contains(search) ||
                    (item.City != null && item.City.Contains(search)));

            return await PaginationHelper.CreateAsync(query, page, perPage.Value, cancellationToken);
        }
        return (object)await _catalogQueryService.GetWarehousesAsync();
    }

    public Task<Warehouse?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => _context.Warehouses.AsNoTracking()
            .Include(item => item.WarehouseType)
            .Include(item => item.Province)
            .Include(item => item.District)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<Warehouse> CreateAsync(Warehouse request, CancellationToken cancellationToken)
    {
        _context.Warehouses.Add(request);
        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(request).Reference(w => w.Province).LoadAsync(cancellationToken);
        await _context.Entry(request).Reference(w => w.District).LoadAsync(cancellationToken);
        return request;
    }

    public async Task<Warehouse> UpdateAsync(int id, Warehouse request, CancellationToken cancellationToken)
    {
        Warehouse entity = await FindAsync(id, cancellationToken);
        entity.Name = string.IsNullOrWhiteSpace(request.Name) ? entity.Name : request.Name;
        entity.Code = string.IsNullOrWhiteSpace(request.Code) ? entity.Code : request.Code;
        entity.City = request.City ?? entity.City;
        entity.Type = request.Type ?? entity.Type;
        entity.IsActive = request.IsActive;
        entity.IsPos = request.IsPos;
        entity.WarehouseTypeId = request.WarehouseTypeId;
        entity.ProvinceId = request.ProvinceId;
        entity.DistrictId = request.DistrictId;
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        Warehouse entity = await FindAsync(id, cancellationToken);
        _context.Warehouses.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Warehouse> FindAsync(int id, CancellationToken cancellationToken)
    {
        Warehouse? entity = await _context.Warehouses.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) throw new AppException("Almacén no encontrado.", 404);
        return entity;
    }
}
