namespace AdminHiitop.Api.Domain.Sales.Entities;

/// <summary>
/// Records every attempt to deliver an invoice to a customer (WhatsApp link, email, download).
/// Status codes: LINK_GENERATED, SENT, FAILED, NOT_SUPPORTED.
/// Channel codes: WHATSAPP, EMAIL, DOWNLOAD.
/// </summary>
public sealed class InvoiceDeliveryLog
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }

    public string ChannelCode { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;

    public string? Recipient { get; set; }
    public string? ExternalUrl { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }

    public Invoice Invoice { get; set; } = null!;
}
