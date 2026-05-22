namespace AdminHiitop.Api.Application.DTOs.Shopify;

// ── List / search responses ───────────────────────────────────────────────────

public sealed class ShopifyProductListResponse
{
    public List<ShopifyProductSummary> Products { get; set; } = [];
    public int                         Total    { get; set; }
}

public sealed class ShopifyProductSummary
{
    public long    Id          { get; set; }
    public string  Title       { get; set; } = "";
    public string? ProductType { get; set; }
    public string? Tags        { get; set; }
    public string? Vendor      { get; set; }
    public string  Status      { get; set; } = "active";
    public string? ImageUrl    { get; set; }
    public int     VariantCount { get; set; }
    public decimal MinPrice    { get; set; }
    public decimal MaxPrice    { get; set; }
    public int     TotalStock  { get; set; }
}

// ── Detail (product + variants) ───────────────────────────────────────────────

public sealed class ShopifyProductDetailResponse
{
    public long    Id          { get; set; }
    public string  Title       { get; set; } = "";
    public string? BodyHtml    { get; set; }
    public string  Status      { get; set; } = "active";
    public string? Handle      { get; set; }
    public string? ProductType { get; set; }
    public string? Tags        { get; set; }
    public string? Vendor      { get; set; }
    public string? ImageUrl    { get; set; }
    public List<ShopifyProductOptionResponse> Options  { get; set; } = [];
    public List<ShopifyVariantResponse>       Variants { get; set; } = [];
    public List<ShopifyImageResponse>         Images   { get; set; } = [];
}

public sealed class ShopifyProductOptionResponse
{
    public long         Id       { get; set; }
    public string       Name     { get; set; } = "";
    public int          Position { get; set; }
    public List<string> Values   { get; set; } = [];
}

public sealed class ShopifyVariantResponse
{
    public long    Id                  { get; set; }
    public long    ProductId           { get; set; }
    public string  Title               { get; set; } = "";
    public string? Sku                 { get; set; }
    public decimal Price               { get; set; }
    public decimal? CompareAtPrice     { get; set; }
    public string? Option1             { get; set; }
    public string? Option2             { get; set; }
    public string? Option3             { get; set; }
    public long    InventoryItemId     { get; set; }
    public int     InventoryQty        { get; set; }
    public int     Position            { get; set; }
    public string? InventoryManagement { get; set; }  // "shopify" | null
}

public sealed class ShopifyImageResponse
{
    public long   Id  { get; set; }
    public string Src { get; set; } = "";
    public string? Alt { get; set; }
}

// ── Locations ─────────────────────────────────────────────────────────────────

public sealed class ShopifyLocationResponse
{
    public long   Id       { get; set; }
    public string Name     { get; set; } = "";
    public bool   Active   { get; set; }
    public string? Address { get; set; }
    public string? City    { get; set; }
}

// ── Create product request ────────────────────────────────────────────────────

public sealed class ShopifyProductCreateRequest
{
    public string  Title       { get; set; } = "";
    public string? BodyHtml    { get; set; }
    public string? ProductType { get; set; }
    public string? Tags        { get; set; }
    public string? Vendor      { get; set; }
    public string  Status      { get; set; } = "active";
    public string? ImageUrl    { get; set; }
    public List<ShopifyVariantCreateRequest> Variants { get; set; } = [];
    public List<string> Options { get; set; } = [];  // option names, e.g. ["Talla", "Color"]
}

public sealed class ShopifyVariantCreateRequest
{
    public string?  Option1        { get; set; }
    public string?  Option2        { get; set; }
    public string?  Option3        { get; set; }
    public string?  Sku            { get; set; }
    public string?  Barcode        { get; set; }
    public decimal  Price          { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public int      Qty            { get; set; }   // legacy single-location qty (kept for compat)
}

// ── Bulk inventory update ─────────────────────────────────────────────────────

public sealed class BulkInventoryUpdateRequest
{
    public long LocationId { get; set; }
    public List<BulkInventoryItem> Items { get; set; } = [];
}

public sealed class BulkInventoryItem
{
    public long InventoryItemId { get; set; }
    public int  Available       { get; set; }  // absolute, not delta
}

public sealed class BulkInventoryUpdateResponse
{
    public int  Updated { get; set; }
    public int  Failed  { get; set; }
    public List<string> Errors { get; set; } = [];
}

// ── Update requests ───────────────────────────────────────────────────────────

public sealed class ShopifyProductUpdateRequest
{
    public string?  Title       { get; set; }
    public string?  BodyHtml    { get; set; }
    public string?  ProductType { get; set; }
    public string?  Tags        { get; set; }
    public string?  Vendor      { get; set; }
    public string?  Status      { get; set; }  // active | draft | archived
    public object?  Images      { get; set; }  // [{ src: "..." }] optional
}

public sealed class ShopifyVariantUpdateRequest
{
    public string?  Sku                 { get; set; }
    public decimal? Price               { get; set; }
    public decimal? CompareAtPrice      { get; set; }
    public string?  Option1             { get; set; }
    public string?  Option2             { get; set; }
    public string?  Option3             { get; set; }
    public string?  InventoryManagement { get; set; }  // "shopify" | null (pass "" to clear)
}

// ── Metrics ───────────────────────────────────────────────────────────────────

public sealed class ShopifyMetricsResponse
{
    public int     TotalOrders       { get; set; }
    public decimal TotalRevenue      { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int     PendingOrders     { get; set; }
    public int     FulfilledOrders   { get; set; }
    public int     CancelledOrders   { get; set; }
    public List<ShopifyTopProduct>   TopProducts  { get; set; } = [];
    public List<ShopifyDailyStat>    DailyStats   { get; set; } = [];
}

public sealed class ShopifyTopProduct
{
    public string  Title    { get; set; } = "";
    public int     Quantity { get; set; }
    public decimal Revenue  { get; set; }
}

public sealed class ShopifyDailyStat
{
    public string  Date    { get; set; } = "";   // yyyy-MM-dd
    public int     Orders  { get; set; }
    public decimal Revenue { get; set; }
}

// ── Collections ───────────────────────────────────────────────────────────────

public sealed class ShopifyCollectionResponse
{
    public long    Id       { get; set; }
    public string  Title    { get; set; } = "";
    public string? Handle   { get; set; }
    public string  Type     { get; set; } = "custom";  // "custom" | "smart"
    public string? ImageUrl { get; set; }
}

public sealed class ShopifyCollectResponse
{
    public long Id           { get; set; }
    public long CollectionId { get; set; }
    public long ProductId    { get; set; }
}

public sealed class ShopifyProductCollectionsUpdateRequest
{
    public List<long> AddCollectionIds { get; set; } = [];
    public List<long> RemoveCollectIds { get; set; } = [];  // collect IDs (not collection IDs)
}

// ── Inventory transfer ────────────────────────────────────────────────────────

public sealed class ShopifyInventoryTransferRequest
{
    public long   ShopifyProductId { get; set; }
    public long   ShopifyVariantId { get; set; }
    public long   InventoryItemId  { get; set; }
    public string ProductTitle     { get; set; } = "";
    public string VariantTitle     { get; set; } = "";
    public long   FromLocationId   { get; set; }
    public long   ToLocationId     { get; set; }
    public int    Quantity         { get; set; }
    public string? Reason          { get; set; }
}

public sealed class ShopifyInventoryTransferResponse
{
    public int    Id               { get; set; }
    public string ProductTitle     { get; set; } = "";
    public string VariantTitle     { get; set; } = "";
    public string FromLocationName { get; set; } = "";
    public string ToLocationName   { get; set; } = "";
    public int    Quantity         { get; set; }
    public string? Reason          { get; set; }
    public DateTime CreatedAt      { get; set; }
    public string? CreatedBy       { get; set; }
}

// ── Image upload ──────────────────────────────────────────────────────────────

public sealed class ShopifyImageUploadRequest
{
    public string Attachment { get; set; } = "";  // base64-encoded image data
    public string Filename   { get; set; } = "";  // original filename with extension
}

// ── Customers ─────────────────────────────────────────────────────────────────

public sealed class ShopifyCustomerResponse
{
    public long    Id            { get; set; }
    public string? Email         { get; set; }
    public string? Name          { get; set; }
    public string? Phone         { get; set; }
    public int     OrdersCount   { get; set; }
    public decimal TotalSpent    { get; set; }
    public string? Tags          { get; set; }
    public string? LastOrderName { get; set; }
    public string? City          { get; set; }
    public string? Province      { get; set; }
    public DateTime CreatedAt    { get; set; }
}

public sealed class ShopifyCustomerListResponse
{
    public List<ShopifyCustomerResponse> Customers    { get; set; } = [];
    public int                           Count        { get; set; }
    public string?                       NextPageInfo { get; set; }
    public string?                       PrevPageInfo { get; set; }
}

// ── Inventory levels (all locations) ─────────────────────────────────────────

public sealed class ShopifyInventoryLevelResponse
{
    public long   InventoryItemId { get; set; }
    public long   VariantId       { get; set; }
    public long   LocationId      { get; set; }
    public string LocationName    { get; set; } = "";
    public int    Available       { get; set; }
}
