using AdminHiitop.Api.Application.DTOs.Customers;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface ICustomerQueryService
{
    Task<IReadOnlyList<CustomerListItemResponse>> GetAsync(string? search);
}
