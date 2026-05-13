using AdminHiitop.Api.Domain.Common;
using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Domain.Catalog.Entities;

public sealed class Province : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<District> Districts { get; set; } = new List<District>();
    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Warehouse> Warehouses { get; set; } = new List<Warehouse>();
}
