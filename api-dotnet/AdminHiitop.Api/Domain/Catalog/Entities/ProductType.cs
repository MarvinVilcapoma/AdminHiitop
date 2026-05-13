using AdminHiitop.Api.Domain.Common;
using AdminHiitop.Api.Domain.Inventory.Entities;
using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Domain.Catalog.Entities;

public sealed class ProductType : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Size> Sizes { get; set; } = new List<Size>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<PromotionItem> PromotionItems { get; set; } = new List<PromotionItem>();
}
