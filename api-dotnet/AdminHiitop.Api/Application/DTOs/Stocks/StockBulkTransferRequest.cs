namespace AdminHiitop.Api.Application.DTOs.Stocks;

public sealed class StockBulkTransferRequest
{
    public int? FromWarehouseId { get; set; }
    public int? ToWarehouseId { get; set; }
    public string? Reason { get; set; }
    public string? Observations { get; set; }
    public IReadOnlyList<StockTransferItemRequest> Items { get; set; } = Array.Empty<StockTransferItemRequest>();
}
