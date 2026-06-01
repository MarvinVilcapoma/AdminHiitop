using AdminHiitop.Api.Domain.Common;

namespace AdminHiitop.Api.Domain.Finance.Entities;

public sealed class FinancialMovement : AuditableEntity
{
    /// <summary>EXPENSE or INCOME</summary>
    public string Type { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public FinancialCategory? Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime MovementDate { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    /// <summary>SALE, FIXED, MANUAL, etc.</summary>
    public string? SourceType { get; set; }
    public int? SourceId { get; set; }
    public bool IsFixedGenerated { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }
}
