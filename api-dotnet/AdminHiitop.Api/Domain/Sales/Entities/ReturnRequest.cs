using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Domain.Common;

namespace AdminHiitop.Api.Domain.Sales.Entities;

/// <summary>
/// Represents a customer-initiated return or exchange request.
/// Status codes: REQUESTED, APPROVED, CREDIT_NOTE_ISSUED, COMPLETED, CANCELLED.
/// ReturnType codes: FULL_REFUND, PARTIAL_REFUND, EXCHANGE_SAME_PRICE,
///                   EXCHANGE_WITH_EXTRA_PAYMENT, EXCHANGE_WITH_REFUND, STORE_CREDIT.
/// </summary>
public sealed class ReturnRequest : AuditableEntity
{
    public int? OrderId { get; set; }
    public int? CustomerId { get; set; }
    public int? OriginalInvoiceId { get; set; }
    public int? CreditNoteInvoiceId { get; set; }

    public string ReturnType { get; set; } = "FULL_REFUND";
    public string Status { get; set; } = "REQUESTED";
    public string? Reason { get; set; }
    public string? Observation { get; set; }

    public decimal TotalReturnedAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal StoreCreditAmount { get; set; }

    public bool RequiresCreditNote { get; set; }
    public bool AutoEmitCreditNote { get; set; }

    public string? ProcessedBy { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }

    public Order? Order { get; set; }
    public Customer? Customer { get; set; }
    public Invoice? OriginalInvoice { get; set; }
    public Invoice? CreditNoteInvoice { get; set; }
    public ICollection<ReturnRequestItem> Items { get; set; } = [];
    public CustomerCredit? CustomerCredit { get; set; }
}
