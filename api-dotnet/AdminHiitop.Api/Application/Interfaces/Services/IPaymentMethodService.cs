using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IPaymentMethodService
{
    Task<object> GetAsync(int? perPage, int page, string? search);
    Task<PaymentMethod?> GetByIdAsync(int id);
    Task<PaymentMethod> CreateAsync(PaymentMethod request);
    Task<PaymentMethod> UpdateAsync(int id, PaymentMethod request);
    Task DeleteAsync(int id);
}
