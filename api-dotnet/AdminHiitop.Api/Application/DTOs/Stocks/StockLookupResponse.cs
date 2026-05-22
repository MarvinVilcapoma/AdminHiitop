namespace AdminHiitop.Api.Application.DTOs.Stocks;

public sealed class StockLookupResponse
{
    public int StockId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public int? ColorId { get; set; }
    public string? ColorName { get; set; }
    public string? Size { get; set; }
    public int AvailableQty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal UnitCost { get; set; }
    public string VariantLabel { get; set; } = string.Empty;

    // Source discriminator — "mysql" (default) | "shopify"
    public string Source { get; set; } = "mysql";

    // Shopify-only fields (null for MySQL items)
    public long? ShopifyVariantId       { get; set; }
    public long? ShopifyProductId       { get; set; }
    public long? ShopifyLocationId      { get; set; }
    public long? ShopifyInventoryItemId { get; set; }
    public string? ImageUrl             { get; set; }
}
