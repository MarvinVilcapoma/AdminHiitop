using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.PaymentMethods;

public sealed class PaymentMethodService : IPaymentMethodService
{
    private readonly ICatalogQueryService _catalogQueryService;
    private readonly AdminHiitopDbContext _context;

    public PaymentMethodService(ICatalogQueryService catalogQueryService, AdminHiitopDbContext context)
    {
        _catalogQueryService = catalogQueryService;
        _context = context;
    }

    public async Task<object> GetAsync(int? perPage, int page, string? search, CancellationToken cancellationToken)
    {
        if (perPage.HasValue)
        {
            IQueryable<PaymentMethod> query = _context.PaymentMethods.AsNoTracking().OrderBy(item => item.Name);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(item => item.Name.Contains(search) || item.Code.Contains(search));
            return await PaginationHelper.CreateAsync(query, page, perPage.Value, cancellationToken);
        }
        return (object)await _catalogQueryService.GetPaymentMethodsAsync();
    }

    public Task<PaymentMethod?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => _context.PaymentMethods.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<PaymentMethod> CreateAsync(PaymentMethod request, CancellationToken cancellationToken)
    {
        _context.PaymentMethods.Add(request);
        await _context.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<PaymentMethod> UpdateAsync(int id, PaymentMethod request, CancellationToken cancellationToken)
    {
        PaymentMethod entity = await FindAsync(id, cancellationToken);
        entity.Name = string.IsNullOrWhiteSpace(request.Name) ? entity.Name : request.Name;
        entity.Code = string.IsNullOrWhiteSpace(request.Code) ? entity.Code : request.Code;
        entity.IsActive = request.IsActive;
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        PaymentMethod entity = await FindAsync(id, cancellationToken);
        _context.PaymentMethods.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<PaymentMethod> FindAsync(int id, CancellationToken cancellationToken)
    {
        PaymentMethod? entity = await _context.PaymentMethods.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) throw new AppException("Método de pago no encontrado.", 404);
        return entity;
    }
}
