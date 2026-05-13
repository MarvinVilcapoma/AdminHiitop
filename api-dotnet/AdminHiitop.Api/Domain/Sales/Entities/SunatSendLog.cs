using AdminHiitop.Api.Domain.Common;

namespace AdminHiitop.Api.Domain.Sales.Entities;

public sealed class SunatSendLog : AuditableEntity
{
    public int InvoiceId { get; set; }
    public string Environment { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Serie { get; set; } = string.Empty;
    public int Correlativo { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? RequestXmlPath { get; set; }
    public string? CdrPath { get; set; }
    public string? ResponseCode { get; set; }
    public string? ResponseDescription { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? SentAt { get; set; }

    public Invoice Invoice { get; set; } = null!;
}
