namespace AdminHiitop.Api.Application.DTOs.Stocks;

public sealed class StockWarehouseReferenceResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Type { get; set; }
}
