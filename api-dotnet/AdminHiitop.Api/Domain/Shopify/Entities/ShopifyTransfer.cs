namespace AdminHiitop.Api.Domain.Shopify.Entities;

public sealed class ShopifyTransfer
{
    public int     Id               { get; set; }
    public long    ShopifyProductId { get; set; }
    public long    ShopifyVariantId { get; set; }
    public long    InventoryItemId  { get; set; }
    public string  ProductTitle     { get; set; } = "";
    public string  VariantTitle     { get; set; } = "";
    public long    FromLocationId   { get; set; }
    public string  FromLocationName { get; set; } = "";
    public long    ToLocationId     { get; set; }
    public string  ToLocationName   { get; set; } = "";
    public int     Quantity         { get; set; }
    public string? Reason           { get; set; }
    public DateTime CreatedAt       { get; set; } = DateTime.UtcNow;
    public string? CreatedBy        { get; set; }  // username/email who performed the transfer
}
