namespace AdminHiitop.Api.Application.DTOs.Products;

public sealed class ProductListItemResponse
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Sku { get; init; }
    public string? ProductTypeName { get; init; }
    public string? CollectionName { get; init; }
    public decimal BasePrice { get; init; }
    public decimal UnitCost { get; init; }
    public bool IsActive { get; init; }
    public int TotalStock { get; init; }
}
