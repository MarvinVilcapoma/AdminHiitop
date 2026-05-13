using System.ComponentModel.DataAnnotations.Schema;
using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Domain.Common;
using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Domain.Inventory.Entities;

public sealed class Product : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public int? ProductTypeId { get; set; }
    public int? CollectionId { get; set; }
    [NotMapped]
    public int? UnitMeasureId { get; set; }
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public decimal UnitCost { get; set; }
    public bool IsActive { get; set; } = true;

    public ProductType? ProductType { get; set; }
    public Collection? Collection { get; set; }
    [NotMapped]
    public UnitMeasure? UnitMeasure { get; set; }
    public ICollection<Stock> Stocks { get; set; } = new List<Stock>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<ProductColor> ProductColors { get; set; } = new List<ProductColor>();
    public ICollection<PromotionItem> PromotionItems { get; set; } = new List<PromotionItem>();
}
