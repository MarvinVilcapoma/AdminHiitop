using AdminHiitop.Api.Application.DTOs.Customers;
using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface ICustomerService
{
    Task<object> GetAsync(string? search);
    Task<Customer?> GetByIdAsync(int id);
    Task<Customer> CreateAsync(Customer request);
    Task<Customer> UpdateAsync(int id, Customer request);
    Task DeleteAsync(int id);
    Task<List<CustomerMetricsResponse>> GetMetricsAsync(int top);
}
