using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Domain.Inventory.Entities;

public sealed class ProductColor
{
    public int ProductId { get; set; }
    public int ColorId { get; set; }
    public int SortOrder { get; set; }

    public Product Product { get; set; } = null!;
    public Color Color { get; set; } = null!;
}
