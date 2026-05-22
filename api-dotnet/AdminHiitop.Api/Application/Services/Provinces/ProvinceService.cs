using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Provinces;

public sealed class ProvinceService : IProvinceService
{
    private readonly AdminHiitopDbContext _context;

    public ProvinceService(AdminHiitopDbContext context) => _context = context;

    public async Task<object> GetAsync(int perPage, int page, string? search)
    {
        var query = _context.Provinces.AsNoTracking().OrderBy(item => item.Name).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(item => item.Name.Contains(search) || item.Code.Contains(search));
        return await PaginationHelper.CreateAsync(query, page, perPage);
    }

    public Task<Province?> GetByIdAsync(int id)
        => _context.Provinces.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);

    public async Task<Province> CreateAsync(Province request)
    {
        _context.Provinces.Add(request);
        await _context.SaveChangesAsync();
        return request;
    }

    public async Task<Province> UpdateAsync(int id, Province request)
    {
        Province entity = await FindAsync(id);
        entity.Name = string.IsNullOrWhiteSpace(request.Name) ? entity.Name : request.Name;
        entity.Code = string.IsNullOrWhiteSpace(request.Code) ? entity.Code : request.Code;
        entity.IsActive = request.IsActive;
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task DeleteAsync(int id)
    {
        Province entity = await FindAsync(id);
        List<District> districts = await _context.Districts
            .Where(d => d.ProvinceId == id)
            .ToListAsync();
        if (districts.Count > 0)
            _context.Districts.RemoveRange(districts);
        _context.Provinces.Remove(entity);
        await _context.SaveChangesAsync();
    }

    private async Task<Province> FindAsync(int id)
    {
        Province? entity = await _context.Provinces.FirstOrDefaultAsync(item => item.Id == id);
        if (entity is null) throw new AppException("Provincia no encontrada.", 404);
        return entity;
    }
}
