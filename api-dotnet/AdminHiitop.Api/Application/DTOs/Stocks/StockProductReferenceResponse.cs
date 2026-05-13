namespace AdminHiitop.Api.Application.DTOs.Stocks;

public sealed class StockProductReferenceResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public StockCatalogReferenceResponse? ProductType { get; set; }
    public StockCatalogReferenceResponse? Collection { get; set; }
}
