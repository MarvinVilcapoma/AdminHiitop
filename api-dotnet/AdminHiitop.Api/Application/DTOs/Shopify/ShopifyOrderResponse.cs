namespace AdminHiitop.Api.Application.DTOs.Shopify;

// ── Mapped response that the frontend receives ────────────────────────────────

public sealed class ShopifyOrderResponse
{
    public long     Id                { get; set; }
    public string   OrderNumber       { get; set; } = "";   // Shopify "#1001"
    public DateTime CreatedAt         { get; set; }
    public DateTime UpdatedAt         { get; set; }
    public string?  FinancialStatus   { get; set; }         // paid, pending, etc.
    public string?  FulfillmentStatus { get; set; }         // fulfilled, null, partial
    public decimal  TotalPrice        { get; set; }
    public string   Currency          { get; set; } = "PEN";
    public string?  CustomerName      { get; set; }
    public string?  CustomerEmail     { get; set; }
    public string?  CustomerPhone     { get; set; }
    public string?  ShippingAddress   { get; set; }
    public string?  Province          { get; set; }
    public string?  City              { get; set; }
    public string?  TrackingNumber    { get; set; }
    public string?  TrackingCompany   { get; set; }
    public string?  TrackingUrl       { get; set; }
    public string?  CustomerDocument   { get; set; }  // DNI/RUC from company field
    public string?  Note              { get; set; }
    public string?  Tags              { get; set; }
    public string?  CancelReason      { get; set; }
    public bool     IsCancelled       { get; set; }
    public decimal  SubtotalPrice     { get; set; }
    public decimal  TotalDiscounts    { get; set; }
    public bool     HasFreeShipping   { get; set; }
    public bool     IsLocalPickup     { get; set; }
    public List<ShopifyDiscountCodeResponse>  DiscountCodes  { get; set; } = [];
    public List<ShopifyShippingLineResponse>  ShippingLines  { get; set; } = [];
    public List<ShopifyOrderItemResponse>     Items          { get; set; } = [];
}

public sealed class ShopifyDiscountCodeResponse
{
    public string  Code   { get; set; } = "";
    public decimal Amount { get; set; }
    public string  Type   { get; set; } = "";   // percentage | fixed_amount | shipping
}

public sealed class ShopifyShippingLineResponse
{
    public string  Title          { get; set; } = "";
    public decimal Price          { get; set; }
    public decimal DiscountedPrice { get; set; }
    public bool    IsFree          => DiscountedPrice == 0m;
}

public sealed class ShopifyOrderItemResponse
{
    public long    Id            { get; set; }
    public string  Title         { get; set; } = "";
    public string? VariantTitle  { get; set; }   // color / talla
    public int     Quantity      { get; set; }
    public decimal Price         { get; set; }
    public string? Sku           { get; set; }
    public string? FulfillmentStatus { get; set; }
}

public sealed class ShopifyOrderListResponse
{
    public List<ShopifyOrderResponse> Orders       { get; set; } = [];
    public int                        Count         { get; set; }
    public string?                    NextPageInfo  { get; set; }
    public string?                    PrevPageInfo  { get; set; }
}
