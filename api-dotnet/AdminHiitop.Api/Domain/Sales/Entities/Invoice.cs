using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Domain.Common;

namespace AdminHiitop.Api.Domain.Sales.Entities;

public sealed class Invoice : AuditableEntity
{
    public int? OrderId { get; set; }
    public int InvoiceSeriesId { get; set; }
    public string DocType { get; set; } = string.Empty;
    public string Serie { get; set; } = string.Empty;
    public int Correlativo { get; set; }
    public string FullNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "draft";
    public string? CustomerDocType { get; set; }
    public string? CustomerDocNumber { get; set; }
    public string? CustomerName { get; set; }
    public string Currency { get; set; } = "PEN";
    public string FormOfPayment { get; set; } = "contado";
    public int? PaymentMethodId { get; set; }
    public decimal MtoOperGravadas { get; set; }
    public decimal MtoIgv { get; set; }
    public decimal ValorVenta { get; set; }
    public decimal SubTotal { get; set; }
    public decimal MtoImpVenta { get; set; }
    public int? SunatCode { get; set; }
    public string? SunatDescription { get; set; }
    public string? SunatNotesJson { get; set; }
    public string? XmlContent { get; set; }
    public string? CdrContent { get; set; }
    public string? NoteMotive { get; set; }
    public string? NoteMotiveDesc { get; set; }
    public string? RefDocType { get; set; }
    public string? RefDocNumber { get; set; }
    public DateTime? RefDocDate { get; set; }
    public string? Observations { get; set; }
    public DateTime IssuedAt { get; set; }
    public int? UserId { get; set; }

    public Order? Order { get; set; }
    public InvoiceSeries InvoiceSeries { get; set; } = null!;
    public PaymentMethod? PaymentMethod { get; set; }
    public User? User { get; set; }
    public ICollection<DailySummaryItem> DailySummaryItems { get; set; } = new List<DailySummaryItem>();
    public ICollection<SunatSendLog> SunatSendLogs { get; set; } = new List<SunatSendLog>();
}
