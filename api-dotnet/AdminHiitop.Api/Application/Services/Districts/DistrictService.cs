using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Districts;

public sealed class DistrictService : IDistrictService
{
    private readonly AdminHiitopDbContext _context;

    public DistrictService(AdminHiitopDbContext context)
    {
        _context = context;
    }

    public async Task<object> GetAsync(int perPage, int page, int? provinceId)
    {
        IQueryable<District> query = _context.Districts
            .AsNoTracking()
            .Include(item => item.Province)
            .OrderBy(item => item.Name);

        if (provinceId.HasValue)
        {
            query = query.Where(item => item.ProvinceId == provinceId.Value);
        }

        return await PaginationHelper.CreateAsync(query, page, perPage);
    }

    public Task<District?> GetByIdAsync(int id)
    {
        return _context.Districts
            .AsNoTracking()
            .Include(item => item.Province)
            .FirstOrDefaultAsync(item => item.Id == id);
    }

    public async Task<District> CreateAsync(District request)
    {
        _context.Districts.Add(request);
        await _context.SaveChangesAsync();
        return request;
    }

    public async Task<District> UpdateAsync(int id, District request)
    {
        District entity = await FindAsync(id);
        entity.Name = string.IsNullOrWhiteSpace(request.Name) ? entity.Name : request.Name;
        entity.Code = string.IsNullOrWhiteSpace(request.Code) ? entity.Code : request.Code;
        entity.IsActive = request.IsActive;
        entity.ProvinceId = request.ProvinceId;
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task DeleteAsync(int id)
    {
        District entity = await FindAsync(id);
        _context.Districts.Remove(entity);
        await _context.SaveChangesAsync();
    }

    private async Task<District> FindAsync(int id)
    {
        District? entity = await _context.Districts.FirstOrDefaultAsync(item => item.Id == id);
        if (entity is null)
        {
            throw new AppException("Distrito no encontrado.", 404);
        }

        return entity;
    }
}
