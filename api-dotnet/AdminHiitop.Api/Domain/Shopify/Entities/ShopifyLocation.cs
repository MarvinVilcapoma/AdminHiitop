namespace AdminHiitop.Api.Domain.Shopify.Entities;

public sealed class ShopifyLocation
{
    public int Id { get; set; }
    public long ShopifyLocationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsPos { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public DateTimeOffset SyncedAt { get; set; }
}
