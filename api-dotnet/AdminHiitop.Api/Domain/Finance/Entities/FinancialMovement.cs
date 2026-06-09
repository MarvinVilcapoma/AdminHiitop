using AdminHiitop.Api.Domain.Common;

namespace AdminHiitop.Api.Domain.Finance.Entities;

public sealed class FinancialMovement : AuditableEntity
{
    /// <summary>INCOME or EXPENSE</summary>
    public string Type { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public FinancialCategory? Category { get; set; }
    public string Description { get; set; } = string.Empty;
    /// <summary>Revenue (INCOME) or expense (EXPENSE) amount.</summary>
    public decimal Amount { get; set; }
    /// <summary>Cost of goods sold. Stored as a snapshot — never changes even if product costs are updated later.</summary>
    public decimal CostAmount { get; set; }
    /// <summary>Amount - CostAmount. Pre-computed at generation time for historical accuracy.</summary>
    public decimal GrossProfitAmount { get; set; }
    public DateTime MovementDate { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    /// <summary>ORDER, WEB_ORDER, POS_SALE, SHOPIFY_ORDER, FIXED, MANUAL, ADJUSTMENT</summary>
    public string? SourceType { get; set; }
    public int? SourceId { get; set; }
    public bool IsFixedGenerated { get; set; }
    /// <summary>True when generated automatically from an order; false for manually created movements.</summary>
    public bool IsAutomatic { get; set; }
    /// <summary>Points to the original movement when this is an adjustment or reversal entry.</summary>
    public int? ParentMovementId { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public ICollection<FinancialMovementItem> Items { get; set; } = new List<FinancialMovementItem>();
}
