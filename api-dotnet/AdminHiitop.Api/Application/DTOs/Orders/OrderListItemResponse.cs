namespace AdminHiitop.Api.Application.DTOs.Orders;

public sealed class OrderListItemResponse
{
    public int Id { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public DateTime OrderDate { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public string? CustomerName { get; init; }
    public decimal Total { get; init; }
    public bool NeedsReceipt { get; init; }
}
