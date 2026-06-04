namespace AdminHiitop.Api.Application.DTOs.Invoices;

// ── WhatsApp ─────────────────────────────────────────────────────────────────

public sealed class InvoiceWhatsAppLinkResponse
{
    public string WhatsAppUrl { get; init; } = string.Empty;
    public string PhoneFormatted { get; init; } = string.Empty;
}

// ── Email via Nubefact ────────────────────────────────────────────────────────

public sealed class SendInvoiceEmailRequest
{
    public string Email { get; set; } = string.Empty;
    public bool SaveEmailToCustomer { get; set; }
}

public sealed class SendInvoiceEmailResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string Provider { get; init; } = "NUBEFACT";
    public bool RequiresFallback { get; init; }
    public string? Error { get; init; }
    public DateTime? SentAt { get; init; }
    /// <summary>Public Nubefact PDF URL — present when RequiresFallback=true so the user can share it manually.</summary>
    public string? PdfUrl { get; init; }
}
