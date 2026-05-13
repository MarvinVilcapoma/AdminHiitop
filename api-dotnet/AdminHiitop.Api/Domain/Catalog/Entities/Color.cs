using AdminHiitop.Api.Domain.Common;
using AdminHiitop.Api.Domain.Inventory.Entities;
using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Domain.Catalog.Entities;

public sealed class Color : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? HexCode { get; set; }
    public string Slug { get; set; } = string.Empty;

    public ICollection<ProductColor> ProductColors { get; set; } = new List<ProductColor>();
    public ICollection<Stock> Stocks { get; set; } = new List<Stock>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
