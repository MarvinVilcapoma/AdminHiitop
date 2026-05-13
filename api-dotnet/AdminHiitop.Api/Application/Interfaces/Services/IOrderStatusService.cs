using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IOrderStatusService
{
    Task<object> GetAsync(int perPage, int page, CancellationToken cancellationToken);
    Task<OrderStatus?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<OrderStatus> CreateAsync(OrderStatus request, CancellationToken cancellationToken);
    Task<OrderStatus> UpdateAsync(int id, OrderStatus request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
