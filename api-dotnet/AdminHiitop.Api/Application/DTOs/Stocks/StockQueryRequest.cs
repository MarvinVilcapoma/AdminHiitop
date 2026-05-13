namespace AdminHiitop.Api.Application.DTOs.Stocks;

public sealed class StockQueryRequest
{
    public int PerPage { get; set; } = 15;
    public int Page { get; set; } = 1;
    public string? Search { get; set; }
    public int? WarehouseId { get; set; }
    public int? ColorId { get; set; }
    public int? ProductTypeId { get; set; }
    public int? CollectionId { get; set; }
    public bool LowStock { get; set; }
}
