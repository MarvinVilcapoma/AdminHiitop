namespace AdminHiitop.Api.Application.DTOs.Stocks;

public sealed class StockSummaryResponse
{
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string? WarehouseType { get; set; }
    public int TotalQuantity { get; set; }
    public int TotalItems { get; set; }
}
