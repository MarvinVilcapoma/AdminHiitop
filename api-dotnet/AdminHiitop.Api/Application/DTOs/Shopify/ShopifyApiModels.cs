using System.Text.Json.Serialization;

namespace AdminHiitop.Api.Application.DTOs.Shopify;

// ── Raw Shopify API models (deserialization only) ─────────────────────────────

public sealed class ShopifyApiOrder
{
    [JsonPropertyName("id")]                 public long    Id               { get; set; }
    [JsonPropertyName("name")]               public string  Name             { get; set; } = "";
    [JsonPropertyName("order_number")]       public int     OrderNumber      { get; set; }
    [JsonPropertyName("created_at")]         public DateTimeOffset CreatedAt { get; set; }
    [JsonPropertyName("updated_at")]         public DateTimeOffset UpdatedAt { get; set; }
    [JsonPropertyName("financial_status")]   public string? FinancialStatus  { get; set; }
    [JsonPropertyName("fulfillment_status")] public string? FulfillmentStatus { get; set; }
    [JsonPropertyName("total_price")]        public string  TotalPrice       { get; set; } = "0.00";
    [JsonPropertyName("subtotal_price")]     public string  SubtotalPrice    { get; set; } = "0.00";
    [JsonPropertyName("total_tax")]          public string  TotalTax         { get; set; } = "0.00";
    [JsonPropertyName("currency")]           public string  Currency         { get; set; } = "PEN";
    [JsonPropertyName("customer")]           public ShopifyApiCustomer?  Customer         { get; set; }
    [JsonPropertyName("shipping_address")]   public ShopifyApiAddress?   ShippingAddress  { get; set; }
    [JsonPropertyName("billing_address")]    public ShopifyApiAddress?   BillingAddress   { get; set; }
    [JsonPropertyName("line_items")]         public List<ShopifyApiLineItem> LineItems   { get; set; } = [];
    [JsonPropertyName("fulfillments")]       public List<ShopifyApiFulfillment> Fulfillments { get; set; } = [];
    [JsonPropertyName("note")]               public string? Note             { get; set; }
    [JsonPropertyName("tags")]               public string? Tags             { get; set; }
    [JsonPropertyName("cancel_reason")]      public string? CancelReason     { get; set; }
    [JsonPropertyName("cancelled_at")]       public DateTimeOffset? CancelledAt { get; set; }
    [JsonPropertyName("total_discounts")]    public string TotalDiscounts   { get; set; } = "0.00";
    [JsonPropertyName("total_shipping_price_set")] public object? TotalShippingPriceSet { get; set; }
    [JsonPropertyName("discount_codes")]     public List<ShopifyApiDiscountCode>  DiscountCodes  { get; set; } = [];
    [JsonPropertyName("shipping_lines")]     public List<ShopifyApiShippingLine>  ShippingLines  { get; set; } = [];
}

public sealed class ShopifyApiCustomer
{
    [JsonPropertyName("id")]              public long    Id            { get; set; }
    [JsonPropertyName("first_name")]      public string? FirstName     { get; set; }
    [JsonPropertyName("last_name")]       public string? LastName      { get; set; }
    [JsonPropertyName("email")]           public string? Email         { get; set; }
    [JsonPropertyName("phone")]           public string? Phone         { get; set; }
    [JsonPropertyName("orders_count")]    public int     OrdersCount   { get; set; }
    [JsonPropertyName("total_spent")]     public string  TotalSpent    { get; set; } = "0.00";
    [JsonPropertyName("tags")]            public string? Tags          { get; set; }
    [JsonPropertyName("created_at")]      public DateTimeOffset CreatedAt { get; set; }
    [JsonPropertyName("last_order_name")] public string? LastOrderName { get; set; }
    [JsonPropertyName("default_address")] public ShopifyApiAddress? DefaultAddress { get; set; }
}

public sealed class ShopifyApiAddress
{
    [JsonPropertyName("first_name")] public string? FirstName { get; set; }
    [JsonPropertyName("last_name")]  public string? LastName  { get; set; }
    // Company field is commonly used to store DNI/RUC in Peruvian stores
    [JsonPropertyName("company")]    public string? Company   { get; set; }
    [JsonPropertyName("address1")]   public string? Address1  { get; set; }
    [JsonPropertyName("address2")]   public string? Address2  { get; set; }
    [JsonPropertyName("city")]       public string? City      { get; set; }
    [JsonPropertyName("province")]   public string? Province  { get; set; }
    [JsonPropertyName("country")]    public string? Country   { get; set; }
    [JsonPropertyName("phone")]      public string? Phone     { get; set; }
    [JsonPropertyName("zip")]        public string? Zip       { get; set; }
}

public sealed class ShopifyApiLineItem
{
    [JsonPropertyName("id")]            public long   Id           { get; set; }
    [JsonPropertyName("product_id")]    public long?  ProductId    { get; set; }
    [JsonPropertyName("variant_id")]    public long?  VariantId    { get; set; }
    [JsonPropertyName("title")]         public string Title        { get; set; } = "";
    [JsonPropertyName("variant_title")] public string? VariantTitle { get; set; }
    [JsonPropertyName("quantity")]      public int    Quantity     { get; set; }
    [JsonPropertyName("price")]         public string Price        { get; set; } = "0.00";
    [JsonPropertyName("sku")]           public string? Sku         { get; set; }
    [JsonPropertyName("fulfillment_status")] public string? FulfillmentStatus { get; set; }
}

public sealed class ShopifyApiFulfillment
{
    [JsonPropertyName("id")]              public long    Id             { get; set; }
    [JsonPropertyName("status")]          public string? Status         { get; set; }
    [JsonPropertyName("tracking_number")] public string? TrackingNumber { get; set; }
    [JsonPropertyName("tracking_company")] public string? TrackingCompany { get; set; }
    [JsonPropertyName("tracking_url")]    public string? TrackingUrl    { get; set; }
    [JsonPropertyName("created_at")]      public DateTimeOffset? CreatedAt { get; set; }
}

public sealed class ShopifyApiDiscountCode
{
    [JsonPropertyName("code")]   public string Code   { get; set; } = "";
    [JsonPropertyName("amount")] public string Amount { get; set; } = "0.00";
    [JsonPropertyName("type")]   public string Type   { get; set; } = "";  // percentage | fixed_amount | shipping
}

public sealed class ShopifyApiShippingLine
{
    [JsonPropertyName("title")]         public string  Title        { get; set; } = "";
    [JsonPropertyName("price")]         public string  Price        { get; set; } = "0.00";
    [JsonPropertyName("code")]          public string? Code         { get; set; }
    [JsonPropertyName("source")]        public string? Source       { get; set; }
    [JsonPropertyName("discounted_price")] public string DiscountedPrice { get; set; } = "0.00";
}

// ── Result wrapper from client ────────────────────────────────────────────────

public sealed class ShopifyOrderListResult
{
    public List<ShopifyApiOrder> Orders       { get; set; } = [];
    public string?               NextPageInfo { get; set; }
    public string?               PrevPageInfo { get; set; }
}

// ── Products ──────────────────────────────────────────────────────────────────

public sealed class ShopifyApiProduct
{
    [JsonPropertyName("id")]           public long    Id          { get; set; }
    [JsonPropertyName("title")]        public string  Title       { get; set; } = "";
    [JsonPropertyName("body_html")]    public string? BodyHtml    { get; set; }
    [JsonPropertyName("status")]       public string  Status      { get; set; } = "active";
    [JsonPropertyName("handle")]       public string? Handle      { get; set; }
    [JsonPropertyName("product_type")] public string? ProductType { get; set; }
    [JsonPropertyName("tags")]         public string? Tags        { get; set; }
    [JsonPropertyName("vendor")]        public string? Vendor    { get; set; }
    [JsonPropertyName("variants")]     public List<ShopifyApiVariant>       Variants { get; set; } = [];
    [JsonPropertyName("options")]      public List<ShopifyApiProductOption> Options  { get; set; } = [];
    [JsonPropertyName("images")]       public List<ShopifyApiImage>         Images   { get; set; } = [];
    [JsonPropertyName("image")]        public ShopifyApiImage?              Image    { get; set; }
}

public sealed class ShopifyApiVariant
{
    [JsonPropertyName("id")]                    public long    Id                  { get; set; }
    [JsonPropertyName("product_id")]            public long    ProductId           { get; set; }
    [JsonPropertyName("title")]                 public string  Title               { get; set; } = "";
    [JsonPropertyName("sku")]                   public string? Sku                 { get; set; }
    [JsonPropertyName("price")]                 public string  Price               { get; set; } = "0.00";
    [JsonPropertyName("compare_at_price")]      public string? CompareAtPrice      { get; set; }
    [JsonPropertyName("option1")]               public string? Option1             { get; set; }
    [JsonPropertyName("option2")]               public string? Option2             { get; set; }
    [JsonPropertyName("option3")]               public string? Option3             { get; set; }
    [JsonPropertyName("inventory_item_id")]     public long    InventoryItemId     { get; set; }
    [JsonPropertyName("inventory_quantity")]    public int     InventoryQuantity   { get; set; }
    [JsonPropertyName("inventory_management")]  public string? InventoryManagement { get; set; }
    [JsonPropertyName("position")]              public int     Position            { get; set; }
}

public sealed class ShopifyApiProductOption
{
    [JsonPropertyName("id")]       public long         Id       { get; set; }
    [JsonPropertyName("name")]     public string       Name     { get; set; } = "";
    [JsonPropertyName("position")] public int          Position { get; set; }
    [JsonPropertyName("values")]   public List<string> Values   { get; set; } = [];
}

public sealed class ShopifyApiImage
{
    [JsonPropertyName("id")]  public long   Id  { get; set; }
    [JsonPropertyName("src")] public string Src { get; set; } = "";
    [JsonPropertyName("alt")] public string? Alt { get; set; }
}

// ── Locations ─────────────────────────────────────────────────────────────────

public sealed class ShopifyApiLocation
{
    [JsonPropertyName("id")]       public long   Id       { get; set; }
    [JsonPropertyName("name")]     public string Name     { get; set; } = "";
    [JsonPropertyName("active")]   public bool   Active   { get; set; }
    [JsonPropertyName("address1")] public string? Address1 { get; set; }
    [JsonPropertyName("city")]     public string? City     { get; set; }
}

// ── Inventory ─────────────────────────────────────────────────────────────────

public sealed class ShopifyApiInventoryLevel
{
    [JsonPropertyName("inventory_item_id")] public long InventoryItemId { get; set; }
    [JsonPropertyName("location_id")]       public long LocationId      { get; set; }
    [JsonPropertyName("available")]         public int? Available        { get; set; }
}

// ── Collections ───────────────────────────────────────────────────────────────

public sealed class ShopifyApiCollection
{
    [JsonPropertyName("id")]    public long    Id     { get; set; }
    [JsonPropertyName("title")] public string  Title  { get; set; } = "";
    [JsonPropertyName("handle")] public string? Handle { get; set; }
    [JsonPropertyName("image")] public ShopifyApiImage? Image { get; set; }
}

public sealed class ShopifyApiCollect
{
    [JsonPropertyName("id")]            public long Id           { get; set; }
    [JsonPropertyName("collection_id")] public long CollectionId { get; set; }
    [JsonPropertyName("product_id")]    public long ProductId    { get; set; }
}

// ── GraphQL search DTOs ───────────────────────────────────────────────────────

public sealed class ShopifyGqlResponse<T>
{
    [JsonPropertyName("data")]   public T?                     Data   { get; set; }
    [JsonPropertyName("errors")] public List<ShopifyGqlError>? Errors { get; set; }
}

public sealed class ShopifyGqlError
{
    [JsonPropertyName("message")] public string? Message { get; set; }
}

public sealed class ShopifyGqlOrdersData
{
    [JsonPropertyName("orders")] public ShopifyGqlConnection<ShopifyGqlOrder> Orders { get; set; } = new();
}

public sealed class ShopifyGqlProductsData
{
    [JsonPropertyName("products")] public ShopifyGqlConnection<ShopifyGqlProduct> Products { get; set; } = new();
}

public sealed class ShopifyGqlConnection<T>
{
    [JsonPropertyName("edges")]    public List<ShopifyGqlEdge<T>> Edges    { get; set; } = [];
    [JsonPropertyName("pageInfo")] public ShopifyGqlPageInfo      PageInfo { get; set; } = new();
}

public sealed class ShopifyGqlEdge<T>
{
    [JsonPropertyName("node")] public T Node { get; set; } = default!;
}

public sealed class ShopifyGqlPageInfo
{
    [JsonPropertyName("hasNextPage")] public bool    HasNextPage { get; set; }
    [JsonPropertyName("endCursor")]   public string? EndCursor   { get; set; }
}

public sealed class ShopifyGqlOrder
{
    [JsonPropertyName("id")]                        public string              Id                      { get; set; } = "";
    [JsonPropertyName("name")]                      public string              Name                    { get; set; } = "";
    [JsonPropertyName("createdAt")]                 public DateTimeOffset      CreatedAt               { get; set; }
    [JsonPropertyName("updatedAt")]                 public DateTimeOffset      UpdatedAt               { get; set; }
    [JsonPropertyName("currencyCode")]              public string              CurrencyCode            { get; set; } = "PEN";
    [JsonPropertyName("displayFinancialStatus")]    public string?             DisplayFinancialStatus  { get; set; }
    [JsonPropertyName("displayFulfillmentStatus")]  public string?             DisplayFulfillmentStatus { get; set; }
    [JsonPropertyName("totalPriceSet")]             public ShopifyGqlMoneySet? TotalPriceSet           { get; set; }
    [JsonPropertyName("subtotalPriceSet")]          public ShopifyGqlMoneySet? SubtotalPriceSet        { get; set; }
    [JsonPropertyName("totalDiscountsSet")]         public ShopifyGqlMoneySet? TotalDiscountsSet       { get; set; }
    [JsonPropertyName("customer")]                  public ShopifyGqlCustomer? Customer                { get; set; }
    [JsonPropertyName("shippingAddress")]           public ShopifyGqlAddress?  ShippingAddress         { get; set; }
    [JsonPropertyName("billingAddress")]            public ShopifyGqlAddress?  BillingAddress          { get; set; }
    [JsonPropertyName("note")]                      public string?             Note                    { get; set; }
    [JsonPropertyName("tags")]                      public List<string>        Tags                    { get; set; } = [];
    [JsonPropertyName("cancelReason")]              public string?             CancelReason            { get; set; }
    [JsonPropertyName("cancelledAt")]               public DateTimeOffset?     CancelledAt             { get; set; }
    [JsonPropertyName("fulfillments")]              public List<ShopifyGqlFulfillment>                        Fulfillments  { get; set; } = [];
    [JsonPropertyName("lineItems")]                 public ShopifyGqlConnection<ShopifyGqlLineItem>           LineItems     { get; set; } = new();
    [JsonPropertyName("shippingLines")]             public ShopifyGqlConnection<ShopifyGqlShippingLine>       ShippingLines { get; set; } = new();
    [JsonPropertyName("discountCodes")]             public List<ShopifyGqlDiscountCode>                       DiscountCodes { get; set; } = [];
}

public sealed class ShopifyGqlMoneySet
{
    [JsonPropertyName("shopMoney")] public ShopifyGqlMoney ShopMoney { get; set; } = new();
}

public sealed class ShopifyGqlMoney
{
    [JsonPropertyName("amount")]       public string Amount       { get; set; } = "0.00";
    [JsonPropertyName("currencyCode")] public string CurrencyCode { get; set; } = "PEN";
}

public sealed class ShopifyGqlCustomer
{
    [JsonPropertyName("firstName")] public string? FirstName { get; set; }
    [JsonPropertyName("lastName")]  public string? LastName  { get; set; }
    [JsonPropertyName("email")]     public string? Email     { get; set; }
    [JsonPropertyName("phone")]     public string? Phone     { get; set; }
}

public sealed class ShopifyGqlAddress
{
    [JsonPropertyName("firstName")] public string? FirstName { get; set; }
    [JsonPropertyName("lastName")]  public string? LastName  { get; set; }
    [JsonPropertyName("company")]   public string? Company   { get; set; }
    [JsonPropertyName("address1")]  public string? Address1  { get; set; }
    [JsonPropertyName("city")]      public string? City      { get; set; }
    [JsonPropertyName("province")]  public string? Province  { get; set; }
    [JsonPropertyName("phone")]     public string? Phone     { get; set; }
}

public sealed class ShopifyGqlFulfillment
{
    [JsonPropertyName("trackingNumber")]  public string? TrackingNumber  { get; set; }
    [JsonPropertyName("trackingCompany")] public string? TrackingCompany { get; set; }
    [JsonPropertyName("trackingUrl")]     public string? TrackingUrl     { get; set; }
}

public sealed class ShopifyGqlLineItem
{
    [JsonPropertyName("id")]                   public string             Id                   { get; set; } = "";
    [JsonPropertyName("title")]                public string             Title                { get; set; } = "";
    [JsonPropertyName("variantTitle")]         public string?            VariantTitle         { get; set; }
    [JsonPropertyName("quantity")]             public int                Quantity             { get; set; }
    [JsonPropertyName("sku")]                  public string?            Sku                  { get; set; }
    [JsonPropertyName("fulfillmentStatus")]    public string?            FulfillmentStatus    { get; set; }
    [JsonPropertyName("originalUnitPriceSet")] public ShopifyGqlMoneySet? OriginalUnitPriceSet { get; set; }
}

public sealed class ShopifyGqlShippingLine
{
    [JsonPropertyName("title")]               public string             Title              { get; set; } = "";
    [JsonPropertyName("originalPriceSet")]    public ShopifyGqlMoneySet? OriginalPriceSet   { get; set; }
    [JsonPropertyName("discountedPriceSet")]  public ShopifyGqlMoneySet? DiscountedPriceSet { get; set; }
}

public sealed class ShopifyGqlDiscountCode
{
    [JsonPropertyName("code")] public string Code { get; set; } = "";
}

public sealed class ShopifyGqlProduct
{
    [JsonPropertyName("id")]              public string                              Id              { get; set; } = "";
    [JsonPropertyName("title")]           public string                              Title           { get; set; } = "";
    [JsonPropertyName("descriptionHtml")] public string?                             DescriptionHtml { get; set; }
    [JsonPropertyName("status")]          public string                              Status          { get; set; } = "ACTIVE";
    [JsonPropertyName("handle")]          public string?                             Handle          { get; set; }
    [JsonPropertyName("productType")]     public string?                             ProductType     { get; set; }
    [JsonPropertyName("tags")]            public List<string>                        Tags            { get; set; } = [];
    [JsonPropertyName("vendor")]          public string?                             Vendor          { get; set; }
    [JsonPropertyName("featuredImage")]   public ShopifyGqlImage?                    FeaturedImage   { get; set; }
    [JsonPropertyName("options")]         public List<ShopifyGqlProductOption>       Options         { get; set; } = [];
    [JsonPropertyName("variants")]        public ShopifyGqlConnection<ShopifyGqlVariant> Variants   { get; set; } = new();
    [JsonPropertyName("images")]          public ShopifyGqlConnection<ShopifyGqlImage>  Images      { get; set; } = new();
}

public sealed class ShopifyGqlImage
{
    [JsonPropertyName("url")]     public string  Url     { get; set; } = "";
    [JsonPropertyName("altText")] public string? AltText { get; set; }
}

public sealed class ShopifyGqlProductOption
{
    [JsonPropertyName("id")]       public string       Id       { get; set; } = "";
    [JsonPropertyName("name")]     public string       Name     { get; set; } = "";
    [JsonPropertyName("position")] public int          Position { get; set; }
    [JsonPropertyName("values")]   public List<string> Values   { get; set; } = [];
}

public sealed class ShopifyGqlVariant
{
    [JsonPropertyName("id")]                public string                        Id                { get; set; } = "";
    [JsonPropertyName("title")]             public string                        Title             { get; set; } = "";
    [JsonPropertyName("sku")]               public string?                       Sku               { get; set; }
    [JsonPropertyName("price")]             public string                        Price             { get; set; } = "0.00";
    [JsonPropertyName("compareAtPrice")]    public string?                       CompareAtPrice    { get; set; }
    [JsonPropertyName("position")]          public int                           Position          { get; set; }
    [JsonPropertyName("inventoryQuantity")] public int                           InventoryQuantity { get; set; }
    [JsonPropertyName("selectedOptions")]   public List<ShopifyGqlSelectedOption> SelectedOptions  { get; set; } = [];
    [JsonPropertyName("inventoryItem")]     public ShopifyGqlInventoryItem?       InventoryItem    { get; set; }
}

public sealed class ShopifyGqlSelectedOption
{
    [JsonPropertyName("name")]  public string Name  { get; set; } = "";
    [JsonPropertyName("value")] public string Value { get; set; } = "";
}

public sealed class ShopifyGqlInventoryItem
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
}

// ── GraphQL lightweight product list DTOs (no variants — lower query cost) ────

public sealed class ShopifyGqlProductsLiteData
{
    [JsonPropertyName("products")] public ShopifyGqlConnection<ShopifyGqlProductLite> Products { get; set; } = new();
}

public sealed class ShopifyGqlProductLite
{
    [JsonPropertyName("id")]              public string              Id             { get; set; } = "";
    [JsonPropertyName("title")]           public string              Title          { get; set; } = "";
    [JsonPropertyName("status")]          public string              Status         { get; set; } = "ACTIVE";
    [JsonPropertyName("productType")]     public string?             ProductType    { get; set; }
    [JsonPropertyName("tags")]            public List<string>        Tags           { get; set; } = [];
    [JsonPropertyName("vendor")]          public string?             Vendor         { get; set; }
    [JsonPropertyName("featuredImage")]   public ShopifyGqlImage?    FeaturedImage  { get; set; }
    [JsonPropertyName("variantsCount")]   public ShopifyGqlCount?    VariantsCount  { get; set; }
    [JsonPropertyName("priceRangeV2")]    public ShopifyGqlPriceRange? PriceRangeV2 { get; set; }
    [JsonPropertyName("totalInventory")]  public int                 TotalInventory { get; set; }
}

public sealed class ShopifyGqlCount
{
    [JsonPropertyName("count")] public int Count { get; set; }
}

public sealed class ShopifyGqlPriceRange
{
    [JsonPropertyName("minVariantPrice")] public ShopifyGqlMoneyV2 MinVariantPrice { get; set; } = new();
    [JsonPropertyName("maxVariantPrice")] public ShopifyGqlMoneyV2 MaxVariantPrice { get; set; } = new();
}

public sealed class ShopifyGqlMoneyV2
{
    [JsonPropertyName("amount")] public string Amount { get; set; } = "0.00";
}
