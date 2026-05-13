using AdminHiitop.Api.Domain.Common;
using AdminHiitop.Api.Domain.Inventory.Entities;
using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Domain.Catalog.Entities;

public sealed class Warehouse : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int? WarehouseTypeId { get; set; }
    public string? Type { get; set; }
    public string? City { get; set; }
    public int? ProvinceId { get; set; }
    public int? DistrictId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPos { get; set; }

    public WarehouseType? WarehouseType { get; set; }
    public Province? Province { get; set; }
    public District? District { get; set; }
    public ICollection<Stock> Stocks { get; set; } = new List<Stock>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}
