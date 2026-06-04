using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Domain.Common;

namespace AdminHiitop.Api.Domain.Sales.Entities;

/// <summary>
/// Store credit balance generated from a return. Status codes: ACTIVE, USED, PARTIALLY_USED, EXPIRED, CANCELLED.
/// </summary>
public sealed class CustomerCredit : AuditableEntity
{
    public int CustomerId { get; set; }
    public int? ReturnRequestId { get; set; }
    public int? CreditNoteInvoiceId { get; set; }

    public decimal Amount { get; set; }
    public decimal UsedAmount { get; set; }
    public decimal RemainingAmount { get; set; }

    public string Status { get; set; } = "ACTIVE";
    public string? Notes { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public Customer Customer { get; set; } = null!;
    public ReturnRequest? ReturnRequest { get; set; }
}
