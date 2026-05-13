namespace AdminHiitop.Api.Application.DTOs.Stocks;

public sealed class StockTransferRequest
{
    public int TargetWarehouseId { get; set; }
    public int Quantity { get; set; }
}
