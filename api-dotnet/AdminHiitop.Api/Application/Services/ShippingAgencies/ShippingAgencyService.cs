using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.ShippingAgencies;

public sealed class ShippingAgencyService : IShippingAgencyService
{
    private readonly AdminHiitopDbContext _context;

    public ShippingAgencyService(AdminHiitopDbContext context) => _context = context;

    public async Task<object> GetAsync(int perPage, int page)
        => await PaginationHelper.CreateAsync(_context.ShippingAgencies.AsNoTracking().OrderBy(item => item.Name), page, perPage);

    public Task<ShippingAgency?> GetByIdAsync(int id)
        => _context.ShippingAgencies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);

    public async Task<ShippingAgency> CreateAsync(ShippingAgency request)
    {
        _context.ShippingAgencies.Add(request);
        await _context.SaveChangesAsync();
        return request;
    }

    public async Task<ShippingAgency> UpdateAsync(int id, ShippingAgency request)
    {
        ShippingAgency entity = await FindAsync(id);
        entity.Name = string.IsNullOrWhiteSpace(request.Name) ? entity.Name : request.Name;
        entity.Code = string.IsNullOrWhiteSpace(request.Code) ? entity.Code : request.Code;
        entity.IsActive = request.IsActive;
        entity.ShippingRate = request.ShippingRate;
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task DeleteAsync(int id)
    {
        ShippingAgency entity = await FindAsync(id);
        _context.ShippingAgencies.Remove(entity);
        await _context.SaveChangesAsync();
    }

    private async Task<ShippingAgency> FindAsync(int id)
    {
        ShippingAgency? entity = await _context.ShippingAgencies.FirstOrDefaultAsync(item => item.Id == id);
        if (entity is null) throw new AppException("Agencia de envío no encontrada.", 404);
        return entity;
    }
}
