using AdminHiitop.Api.Application.DTOs.Orders;
using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IOrderService
{
    Task<object> GetAsync(string? search, int? perPage, int page, bool withSummary, int? orderStatusId, int? userId = null, string? source = null);
    Task<Order?> GetByIdAsync(int id);
    Task<Order> CreateAsync(OrderUpsertRequest request);
    Task<Order> UpdateAsync(int id, OrderUpsertRequest request);
    Task<Order> UpdateTrackingAsync(int id, OrderTrackingUpdateRequest request);
    Task<Order> ChangeStatus(int id, int orderStatusId);
    Task DeleteAsync(int id);
    Task<IReadOnlyList<OrderMonthlyStat>> GetMonthlyStatsAsync(int year);
}

public sealed class OrderMonthlyStat
{
    public int     Year    { get; set; }
    public int     Month   { get; set; }
    public string  Label   { get; set; } = "";   // "Ene", "Feb", …
    public int     Orders  { get; set; }
    public decimal Revenue { get; set; }
}
