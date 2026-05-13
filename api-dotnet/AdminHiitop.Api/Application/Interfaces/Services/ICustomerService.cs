using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface ICustomerService
{
    Task<object> GetAsync(string? search, CancellationToken cancellationToken);
    Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<Customer> CreateAsync(Customer request, CancellationToken cancellationToken);
    Task<Customer> UpdateAsync(int id, Customer request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
