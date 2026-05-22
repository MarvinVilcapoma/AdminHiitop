using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IOrderStatusService
{
    Task<object> GetAsync(int perPage, int page);
    Task<OrderStatus?> GetByIdAsync(int id);
    Task<OrderStatus> CreateAsync(OrderStatus request);
    Task<OrderStatus> UpdateAsync(int id, OrderStatus request);
    Task DeleteAsync(int id);
}
