using AdminHiitop.Api.Domain.Common;

namespace AdminHiitop.Api.Domain.Finance.Entities;

public sealed class FixedFinancialMovement : AuditableEntity
{
    /// <summary>EXPENSE or INCOME</summary>
    public string Type { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public FinancialCategory? Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    /// <summary>MONTHLY, WEEKLY, YEARLY</summary>
    public string Frequency { get; set; } = "MONTHLY";
    public int? DayOfMonth { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? PaymentMethod { get; set; }
    public bool AutoGenerate { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }
}
