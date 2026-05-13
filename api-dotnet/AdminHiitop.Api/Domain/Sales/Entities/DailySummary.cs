using AdminHiitop.Api.Domain.Common;

namespace AdminHiitop.Api.Domain.Sales.Entities;

public sealed class DailySummary : AuditableEntity
{
    public DateTime SummaryDate { get; set; }
    public string SummaryNumber { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string Status { get; set; } = "generated";
    public string? Ticket { get; set; }
    public string? XmlContent { get; set; }
    public string? CdrContent { get; set; }
    public int? SunatCode { get; set; }
    public string? SunatDescription { get; set; }
    public string? SunatNotesJson { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? RejectedAt { get; set; }

    public ICollection<DailySummaryItem> Items { get; set; } = new List<DailySummaryItem>();
}
