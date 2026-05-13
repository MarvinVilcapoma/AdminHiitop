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

    public async Task<object> GetAsync(int perPage, int page, CancellationToken cancellationToken)
        => await PaginationHelper.CreateAsync(_context.Provinces.AsNoTracking().OrderBy(item => item.Name), page, perPage, cancellationToken);

    public Task<Province?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => _context.Provinces.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<Province> CreateAsync(Province request, CancellationToken cancellationToken)
    {
        _context.Provinces.Add(request);
        await _context.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<Province> UpdateAsync(int id, Province request, CancellationToken cancellationToken)
    {
        Province entity = await FindAsync(id, cancellationToken);
        entity.Name = string.IsNullOrWhiteSpace(request.Name) ? entity.Name : request.Name;
        entity.Code = string.IsNullOrWhiteSpace(request.Code) ? entity.Code : request.Code;
        entity.IsActive = request.IsActive;
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        Province entity = await FindAsync(id, cancellationToken);
        _context.Provinces.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Province> FindAsync(int id, CancellationToken cancellationToken)
    {
        Province? entity = await _context.Provinces.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) throw new AppException("Provincia no encontrada.", 404);
        return entity;
    }
}
