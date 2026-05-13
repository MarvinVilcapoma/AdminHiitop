using AdminHiitop.Api.Domain.Common;
using AdminHiitop.Api.Domain.Inventory.Entities;
using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Domain.Catalog.Entities;

public sealed class Collection : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
