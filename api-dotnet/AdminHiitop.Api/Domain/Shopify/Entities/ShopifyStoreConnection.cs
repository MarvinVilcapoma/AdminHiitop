namespace AdminHiitop.Api.Domain.Shopify.Entities;

public sealed class ShopifyStoreConnection
{
    public int      Id           { get; set; }
    public string   ShopDomain   { get; set; } = "";   // e.g. hiitop-3136.myshopify.com
    public string   AccessToken  { get; set; } = "";   // offline permanent token from OAuth
    public string   Scope        { get; set; } = "";   // granted scopes CSV
    public DateTime InstalledAt  { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt    { get; set; } = DateTime.UtcNow;
}
