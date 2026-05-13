using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Domain.Common;
using AdminHiitop.Api.Domain.Inventory.Entities;

namespace AdminHiitop.Api.Domain.Sales.Entities;

public sealed class PromotionItem : AuditableEntity
{
    public int PromotionId { get; set; }
    public int? ProductTypeId { get; set; }
    public int? ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? Notes { get; set; }

    public Promotion Promotion { get; set; } = null!;
    public ProductType? ProductType { get; set; }
    public Product? Product { get; set; }
}
