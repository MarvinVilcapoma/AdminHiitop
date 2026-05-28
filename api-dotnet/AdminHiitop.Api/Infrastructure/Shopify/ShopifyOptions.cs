namespace AdminHiitop.Api.Infrastructure.Shopify;

public sealed class ShopifyOptions
{
    public const string SectionName = "Shopify";

    public string ShopDomain    { get; set; } = string.Empty;   // hiitop-3136.myshopify.com
    public string ClientId      { get; set; } = string.Empty;
    public string ClientSecret  { get; set; } = string.Empty;   // OAuth secret (shpss_...)
    // Direct admin API access token (shpat_...) from Custom App — permanent, skips OAuth entirely.
    // If set, ClientId/ClientSecret are ignored for authentication.
    public string AccessToken   { get; set; } = string.Empty;
    public string ApiVersion    { get; set; } = "2026-04";
    public int    TimeoutSeconds { get; set; } = 30;

    // Inventory integration flags
    public bool   SyncInventory    { get; set; } = false;  // push stock changes to Shopify
    public bool   UseShopifyStock  { get; set; } = false;  // read available stock from Shopify

    // Default Shopify location ID used for inventory queries.
    // Leave 0 to auto-detect the first active location.
    public long   DefaultLocationId { get; set; } = 0;

    // When true the entire app runs in "Shopify mode":
    //   - products, warehouses and inventory come from Shopify
    //   - local stock/product CRUD views are hidden in the frontend
    //   - orders are still saved to the local DB
    public bool   UseShopifyMode { get; set; } = false;

    // OAuth flow settings — only needed when using authorization_code flow (no static AccessToken).
    // PublicApiBaseUrl: publicly reachable URL of this API (e.g. https://api.hiitop.com)
    // FrontendRedirectUrl: page in the Angular app to land on after successful OAuth install
    // Scopes: comma-separated list of Shopify API scopes to request
    public string PublicApiBaseUrl     { get; set; } = string.Empty;
    public string FrontendRedirectUrl  { get; set; } = string.Empty;
    public string Scopes               { get; set; } = "read_products,write_products,read_inventory,write_inventory,read_orders,write_orders";
}
