using AdminHiitop.Api.Domain.Common;

namespace AdminHiitop.Api.Domain.Finance.Entities;

/// <summary>
/// Represents an initial or additional investment made in the business.
/// Investments are separate from operational expenses and are used to
/// calculate return on investment (ROI) and investment recovery percentage.
/// </summary>
public sealed class Investment : AuditableEntity
{
    public int InvestmentCategoryId { get; set; }
    public InvestmentCategory? InvestmentCategory { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime InvestmentDate { get; set; }
    public bool IsActive { get; set; } = true;
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }
}
