namespace AdminHiitop.Api.Application.DTOs.Stocks;

public sealed class StockBulkTransferRequest
{
    public IReadOnlyList<StockTransferItemRequest> Items { get; set; } = Array.Empty<StockTransferItemRequest>();
}
