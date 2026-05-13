using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Domain.Common;
using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Domain.Inventory.Entities;

public sealed class StockMovement : AuditableEntity
{
    public int StockId { get; set; }
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public int? ColorId { get; set; }
    public string? Size { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int PreviousQuantity { get; set; }
    public int NewQuantity { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public int? UserId { get; set; }

    public Stock Stock { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public Color? Color { get; set; }
    public User? User { get; set; }
}
