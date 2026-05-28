namespace AdminHiitop.Api.Application.DTOs.Stocks;

public sealed class StockUpsertRequest
{
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public int? ColorId { get; set; }
    public string? Size { get; set; }
    public int Quantity { get; set; }
    public int Reserved { get; set; }
    public string? MovementType { get; set; }
    public string? SubMovementType { get; set; }
    public string? Reason { get; set; }
}
