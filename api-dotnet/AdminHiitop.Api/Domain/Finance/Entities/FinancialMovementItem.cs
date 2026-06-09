using AdminHiitop.Api.Domain.Common;

namespace AdminHiitop.Api.Domain.Finance.Entities;

/// <summary>
/// Per-product line snapshot of a financial movement.
/// Prices and costs are frozen at the time of sale so that historical reports
/// remain accurate even when product costs or prices are later updated.
/// </summary>
public sealed class FinancialMovementItem : AuditableEntity
{
    public int FinancialMovementId { get; set; }
    public FinancialMovement? FinancialMovement { get; set; }

    public int? ProductId { get; set; }
    public string? ProductCode { get; set; }
    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }
    /// <summary>Unit selling price at the time of sale (snapshot).</summary>
    public decimal UnitSalePrice { get; set; }
    /// <summary>Unit cost at the time of sale (snapshot). Never changes after creation.</summary>
    public decimal UnitCostSnapshot { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalSaleAmount { get; set; }
    public decimal TotalCostAmount { get; set; }
    public decimal GrossProfitAmount { get; set; }
    /// <summary>True when unit cost was not available at sync time. Profit figures will be incomplete.</summary>
    public bool IsCostPending { get; set; }
}
