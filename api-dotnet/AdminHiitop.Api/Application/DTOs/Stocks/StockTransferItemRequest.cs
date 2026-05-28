namespace AdminHiitop.Api.Application.DTOs.Stocks;

public sealed class StockTransferItemRequest
{
    public int StockId { get; set; }
    public int TargetWarehouseId { get; set; }
    public int? ProductId { get; set; }
    public int? ColorId { get; set; }
    public string? Size { get; set; }
    public int Quantity { get; set; }
}
