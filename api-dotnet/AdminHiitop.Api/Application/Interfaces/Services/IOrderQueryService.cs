using AdminHiitop.Api.Application.DTOs.Orders;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IOrderQueryService
{
    Task<IReadOnlyList<OrderListItemResponse>> GetAsync(string? search);
}
