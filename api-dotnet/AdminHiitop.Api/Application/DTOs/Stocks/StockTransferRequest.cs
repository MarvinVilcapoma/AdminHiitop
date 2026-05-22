namespace AdminHiitop.Api.Application.DTOs.Stocks;

public sealed class StockTransferRequest
{
    //public int TargetWarehouseId { get; set; }
    public int DestinationWarehouseId { get; set; }
    public int Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
}
