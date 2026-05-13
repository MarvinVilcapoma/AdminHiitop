using AdminHiitop.Api.Domain.Common;

namespace AdminHiitop.Api.Domain.Sales.Entities;

public sealed class DailySummaryItem : AuditableEntity
{
    public int DailySummaryId { get; set; }
    public int InvoiceId { get; set; }
    public string DocType { get; set; } = string.Empty;
    public string Serie { get; set; } = string.Empty;
    public int Correlativo { get; set; }
    public string? CustomerDocType { get; set; }
    public string? CustomerDocNumber { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = "generated";

    public DailySummary DailySummary { get; set; } = null!;
    public Invoice Invoice { get; set; } = null!;
}
