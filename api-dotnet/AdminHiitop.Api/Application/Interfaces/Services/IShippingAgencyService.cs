using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IShippingAgencyService
{
    Task<object> GetAsync(int perPage, int page, CancellationToken cancellationToken);
    Task<ShippingAgency?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<ShippingAgency> CreateAsync(ShippingAgency request, CancellationToken cancellationToken);
    Task<ShippingAgency> UpdateAsync(int id, ShippingAgency request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
