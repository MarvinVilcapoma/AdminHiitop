using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IPaymentMethodService
{
    Task<object> GetAsync(int? perPage, int page, string? search, CancellationToken cancellationToken);
    Task<PaymentMethod?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<PaymentMethod> CreateAsync(PaymentMethod request, CancellationToken cancellationToken);
    Task<PaymentMethod> UpdateAsync(int id, PaymentMethod request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
