using AdminHiitop.Api.Application.DTOs.Invoices;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IInvoiceDeliveryService
{
    /// <summary>
    /// Builds a WhatsApp Click-to-Chat URL (wa.me) with a pre-filled message that includes
    /// the invoice details and the PDF link. Does not send automatically.
    /// </summary>
    Task<InvoiceWhatsAppLinkResponse> GetWhatsAppLinkAsync(int invoiceId, string phone, string countryCode = "51");

    /// <summary>
    /// Re-sends the invoice to the customer's email by calling Nubefact's API with
    /// <c>enviar_automaticamente_al_cliente = true</c>. Nubefact returns the existing
    /// document and triggers the email delivery on their side.
    /// </summary>
    Task<SendInvoiceEmailResponse> SendEmailViaNubefactAsync(int invoiceId, SendInvoiceEmailRequest request);
}
