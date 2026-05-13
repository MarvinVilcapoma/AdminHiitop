namespace AdminHiitop.Api.Application.DTOs.Stocks;

public sealed class StockResponse
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public int? ColorId { get; set; }
    public string? Size { get; set; }
    public int Quantity { get; set; }
    public int Reserved { get; set; }
    public int Available { get; set; }
    public StockProductReferenceResponse? Product { get; set; }
    public StockWarehouseReferenceResponse? Warehouse { get; set; }
    public StockColorReferenceResponse? Color { get; set; }
}
