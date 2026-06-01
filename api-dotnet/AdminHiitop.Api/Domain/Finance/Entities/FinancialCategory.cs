using AdminHiitop.Api.Domain.Common;

namespace AdminHiitop.Api.Domain.Finance.Entities;

public sealed class FinancialCategory : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    /// <summary>EXPENSE or INCOME</summary>
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public bool IsActive { get; set; } = true;
}
