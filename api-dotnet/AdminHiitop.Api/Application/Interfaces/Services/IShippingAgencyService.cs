using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IShippingAgencyService
{
    Task<object> GetAsync(int perPage, int page);
    Task<ShippingAgency?> GetByIdAsync(int id);
    Task<ShippingAgency> CreateAsync(ShippingAgency request);
    Task<ShippingAgency> UpdateAsync(int id, ShippingAgency request);
    Task DeleteAsync(int id);
}
