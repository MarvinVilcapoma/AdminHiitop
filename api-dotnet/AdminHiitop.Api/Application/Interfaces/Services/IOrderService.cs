using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IOrderService
{
    Task<object> GetAsync(string? search, int? perPage, int page, bool withSummary, int? orderStatusId);
    Task<Order?> GetByIdAsync(int id);
    Task<Order> CreateAsync(Order request);
    Task<Order> UpdateAsync(int id, Order request);
    Task DeleteAsync(int id);
}
