using AdminHiitop.Api.Domain.Common;

namespace AdminHiitop.Api.Domain.Sales.Entities;

public sealed class SaleImport : AuditableEntity
{
    public string BatchCode { get; set; } = string.Empty;
    public string SourceFileName { get; set; } = string.Empty;
    public int ImportedRows { get; set; }
    public int ImportedByUserId { get; set; }
    public DateTime ImportedAt { get; set; }
}
