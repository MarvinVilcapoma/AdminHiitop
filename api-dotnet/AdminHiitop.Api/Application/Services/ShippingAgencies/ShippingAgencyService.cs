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

    public async Task<object> GetAsync(int perPage, int page, CancellationToken cancellationToken)
        => await PaginationHelper.CreateAsync(_context.ShippingAgencies.AsNoTracking().OrderBy(item => item.Name), page, perPage, cancellationToken);

    public Task<ShippingAgency?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => _context.ShippingAgencies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<ShippingAgency> CreateAsync(ShippingAgency request, CancellationToken cancellationToken)
    {
        _context.ShippingAgencies.Add(request);
        await _context.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<ShippingAgency> UpdateAsync(int id, ShippingAgency request, CancellationToken cancellationToken)
    {
        ShippingAgency entity = await FindAsync(id, cancellationToken);
        entity.Name = string.IsNullOrWhiteSpace(request.Name) ? entity.Name : request.Name;
        entity.Code = string.IsNullOrWhiteSpace(request.Code) ? entity.Code : request.Code;
        entity.IsActive = request.IsActive;
        entity.ShippingRate = request.ShippingRate;
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        ShippingAgency entity = await FindAsync(id, cancellationToken);
        _context.ShippingAgencies.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<ShippingAgency> FindAsync(int id, CancellationToken cancellationToken)
    {
        ShippingAgency? entity = await _context.ShippingAgencies.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) throw new AppException("Agencia de envío no encontrada.", 404);
        return entity;
    }
}
