using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Domain.Common;

namespace AdminHiitop.Api.Domain.Inventory.Entities;

public sealed class Stock : AuditableEntity
{
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public int? ColorId { get; set; }
    public string? Size { get; set; }
    public int Quantity { get; set; }
    public int Reserved { get; set; }

    public Product Product { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public Color? Color { get; set; }
}
