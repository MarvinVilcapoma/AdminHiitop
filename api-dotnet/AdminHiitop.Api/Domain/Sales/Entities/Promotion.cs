using AdminHiitop.Api.Domain.Common;

namespace AdminHiitop.Api.Domain.Sales.Entities;

public sealed class Promotion : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal? FixedPrice { get; set; }

    public ICollection<PromotionItem> Items { get; set; } = new List<PromotionItem>();
}
