namespace AdminHiitop.Api.Application.DTOs.Products;

public sealed class ProductUpsertRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public int? ProductTypeId { get; set; }
    public int? CollectionId { get; set; }
    public int? UnitMeasureId { get; set; }
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public decimal UnitCost { get; set; }
    public bool IsActive { get; set; } = true;
}
