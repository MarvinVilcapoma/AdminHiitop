namespace AdminHiitop.Api.Application.DTOs.Returns;

// ── Create ────────────────────────────────────────────────────────────────────

public sealed class CreateReturnRequest
{
    public int OrderId { get; set; }
    public int? CustomerId { get; set; }
    public int? OriginalInvoiceId { get; set; }

    /// <summary>FULL_REFUND, PARTIAL_REFUND, EXCHANGE_SAME_PRICE, EXCHANGE_WITH_EXTRA_PAYMENT,
    /// EXCHANGE_WITH_REFUND, STORE_CREDIT</summary>
    public string ReturnType { get; set; } = "FULL_REFUND";
    public string? Reason { get; set; }
    public string? Observation { get; set; }

    /// <summary>When true, emit the credit note via Nubefact immediately.</summary>
    public bool AutoEmitCreditNote { get; set; } = true;
    /// <summary>SUNAT credit note reason code. '06' = Devolución total, '07' = Devolución por ítem.</summary>
    public string NoteMotive { get; set; } = "06";
    public string? NoteMotiveDesc { get; set; }

    public List<CreateReturnItemRequest> Items { get; set; } = [];
}

public sealed class CreateReturnItemRequest
{
    public int? OrderItemId { get; set; }
    public int? ProductId { get; set; }
    public int? StockId { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public string? ProductDescription { get; set; }
    /// <summary>NEW, USED, DAMAGED, DEFECTIVE</summary>
    public string Condition { get; set; } = "USED";
    /// <summary>RETURN_TO_STOCK, SEND_TO_REVIEW, DO_NOT_RESTOCK</summary>
    public string RestockAction { get; set; } = "RETURN_TO_STOCK";
    public string? Reason { get; set; }
}

// ── Responses ─────────────────────────────────────────────────────────────────

public sealed class ReturnRequestResponse
{
    public int Id { get; init; }
    public int? OrderId { get; init; }
    public int? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerDni { get; init; }
    public int? OriginalInvoiceId { get; init; }
    public string? OriginalInvoiceNumber { get; init; }
    public int? CreditNoteInvoiceId { get; init; }
    public string? CreditNoteNumber { get; init; }
    public string ReturnType { get; init; } = string.Empty;
    public string ReturnTypeLabel { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public string? Observation { get; init; }
    public decimal TotalReturnedAmount { get; init; }
    public decimal RefundAmount { get; init; }
    public decimal StoreCreditAmount { get; init; }
    public bool RequiresCreditNote { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public List<ReturnItemResponse> Items { get; init; } = [];
    public string? CreditNotePdfUrl { get; init; }
    public string? CreditNoteSunatStatus { get; init; }
    /// <summary>Items that couldn't be restocked (no matching stock record found).</summary>
    public List<string> StockWarnings { get; init; } = [];
}

public sealed class ReturnItemResponse
{
    public int Id { get; init; }
    public int? OrderItemId { get; init; }
    public int? ProductId { get; init; }
    public string? ProductDescription { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal TotalAmount { get; init; }
    public string Condition { get; init; } = string.Empty;
    public string RestockAction { get; init; } = string.Empty;
    public string? Reason { get; init; }
}
