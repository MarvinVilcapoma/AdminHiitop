using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Domain.Common;
using AdminHiitop.Api.Domain.Inventory.Entities;

namespace AdminHiitop.Api.Domain.Sales.Entities;

public sealed class OrderItem : AuditableEntity
{
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int? ColorId { get; set; }
    public int? CollectionId { get; set; }
    public string? ProductDescription { get; set; }
    public string? Size { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
    public int SortOrder { get; set; }

    public Order Order { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public Color? Color { get; set; }
    public Collection? Collection { get; set; }
}
