using AdminHiitop.Api.Domain.Common;

namespace AdminHiitop.Api.Domain.Catalog.Entities;

public sealed class WarehouseType : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Warehouse> Warehouses { get; set; } = new List<Warehouse>();
}
