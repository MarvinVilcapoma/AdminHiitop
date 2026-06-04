using System.Text.RegularExpressions;
using AdminHiitop.Api.Application.DTOs.ElectronicBilling;
using AdminHiitop.Api.Application.DTOs.Invoices;
using AdminHiitop.Api.Application.Helpers;
using AdminHiitop.Api.Application.Interfaces.ElectronicBilling;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Invoices;

/// <summary>
/// Handles customer delivery of invoices via WhatsApp (Click-to-Chat URL) and email (via Nubefact API).
///
/// This implementation only generates a safe WhatsApp Click-to-Chat URL.
/// It does not automate WhatsApp Web or send messages without user interaction.
///
/// Future extension point: implement IWhatsAppSender using WhatsApp Business Cloud API
/// to support automated sending without requiring the user to press Send manually.
/// </summary>
public sealed class InvoiceDeliveryService : IInvoiceDeliveryService
{
    private readonly AdminHiitopDbContext _context;
    private readonly IElectronicBillingProvider _billingProvider;

    private static readonly Regex DigitsOnly = new(@"\D", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> DocTypeLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["01"] = "Factura",
        ["03"] = "Boleta de venta",
        ["07"] = "Nota de crédito",
        ["08"] = "Nota de débito",
    };

    private readonly IEmailService _emailService;

    public InvoiceDeliveryService(
        AdminHiitopDbContext context,
        IElectronicBillingProvider billingProvider,
        IEmailService emailService)
    {
        _context = context;
        _billingProvider = billingProvider;
        _emailService = emailService;
    }

    // ── WhatsApp ──────────────────────────────────────────────────────────────

    public async Task<InvoiceWhatsAppLinkResponse> GetWhatsAppLinkAsync(
        int invoiceId, string phone, string countryCode = "51")
    {
        Invoice invoice = await _context.Invoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invoiceId)
            ?? throw new AppException("Comprobante no encontrado.", 404);

        if (string.IsNullOrWhiteSpace(phone))
            throw new AppException("El número de WhatsApp es obligatorio.", 422);

        // Resolve best PDF URL: column > enlace_del_pdf from stored JSON > enlace + ".pdf"
        string? pdfUrl = invoice.PdfUrl;
        if (string.IsNullOrWhiteSpace(pdfUrl))
        {
            var stored = NubeFactStorageHelper.ReadStoredResponse(invoice.SunatNotesJson);
            pdfUrl = stored?.EnlaceDelPdf;
            if (string.IsNullOrWhiteSpace(pdfUrl) && !string.IsNullOrWhiteSpace(stored?.Url))
                pdfUrl = stored.Url.TrimEnd('/') + ".pdf";
        }

        if (string.IsNullOrWhiteSpace(pdfUrl))
            throw new AppException("El comprobante aún no tiene PDF disponible. Envíalo a SUNAT primero.", 422);

        string normalizedPhone = NormalizePhone(phone, countryCode);

        string docTypeLabel = DocTypeLabels.TryGetValue(invoice.DocType, out string? label)
            ? label : "comprobante";

        string message = BuildWhatsAppMessage(
            invoice.CustomerName ?? "cliente",
            docTypeLabel,
            invoice.FullNumber,
            pdfUrl);

        string whatsAppUrl = $"https://wa.me/{normalizedPhone}?text={Uri.EscapeDataString(message)}";

        await SaveDeliveryLogAsync(invoice.Id, "WHATSAPP", "LINK_GENERATED", normalizedPhone, whatsAppUrl);

        return new InvoiceWhatsAppLinkResponse
        {
            WhatsAppUrl = whatsAppUrl,
            PhoneFormatted = normalizedPhone
        };
    }

    // ── Email via Nubefact ────────────────────────────────────────────────────

    public async Task<SendInvoiceEmailResponse> SendEmailViaNubefactAsync(
        int invoiceId, SendInvoiceEmailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            throw new AppException("El correo electrónico no es válido.", 422);

        // Load with full navigation properties needed to rebuild the NubeFact request
        Invoice? invoice = await _context.Invoices
            .Include(i => i.Order)
                .ThenInclude(o => o!.Items)
            .Include(i => i.Order!.Customer)
            .FirstOrDefaultAsync(i => i.Id == invoiceId)
            ?? throw new AppException("Comprobante no encontrado.", 404);

        // Only SUNAT documents can be sent via Nubefact
        if (!new[] { "01", "03", "07", "08" }.Contains(invoice.DocType))
        {
            throw new AppException(
                "Solo se pueden enviar por correo comprobantes electrónicos (facturas, boletas y notas). " +
                "Los documentos internos deben enviarse por correo propio.", 422);
        }

        if (invoice.Status is "draft" or "generated")
        {
            throw new AppException(
                "El comprobante aún no ha sido enviado a SUNAT. Envíalo primero antes de reenviar por correo.", 422);
        }

        // Use Nubefact's "enviar_correo" operation to resend the existing document by email.
        // This is the correct API operation — do NOT use "generar_comprobante" for resends.
        var emailRequest = new AdminHiitop.Api.Application.DTOs.ElectronicBilling.NubeFactSendEmailRequest
        {
            TipoDeComprobante = MapDocTypeForNubefact(invoice.DocType),
            Serie             = invoice.Serie,
            Numero            = invoice.Correlativo,
            CorreoElectronico = request.Email
        };

        NubeFactSubmitResult result;
        try
        {
            result = await _billingProvider.SendDocumentByEmailAsync(emailRequest);
        }
        catch (Exception ex)
        {
            await SaveDeliveryLogAsync(invoice.Id, "EMAIL", "FAILED", request.Email, null,
                ex.InnerException?.Message ?? ex.Message);

            return new SendInvoiceEmailResponse
            {
                Success = false,
                Message = "No se pudo conectar con Nubefact para enviar el correo.",
                Error = ex.InnerException?.Message ?? ex.Message
            };
        }

        if (!result.Success)
        {
            string errorMsg = result.Response.Errors ?? result.Response.SunatDescription ?? "Error desconocido de Nubefact.";

            var stored = NubeFactStorageHelper.ReadStoredResponse(invoice.SunatNotesJson);
            string? pdfUrl = invoice.PdfUrl
                ?? stored?.EnlaceDelPdf
                ?? (stored?.Url is not null ? stored.Url.TrimEnd('/') + ".pdf" : null);

            // Try SMTP as automatic fallback when configured
            if (_emailService.IsConfigured && pdfUrl is not null)
            {
                try
                {
                    string docLabel = DocTypeLabels.TryGetValue(invoice.DocType, out string? lbl) ? lbl : "comprobante";
                    string subject  = $"Tu {docLabel} {invoice.FullNumber} — Hiitop";
                    string body     = BuildEmailHtml(invoice.CustomerName ?? "cliente", docLabel, invoice.FullNumber, pdfUrl);
                    await _emailService.SendAsync(request.Email, subject, body);

                    await SaveDeliveryLogAsync(invoice.Id, "EMAIL", "SENT", request.Email, pdfUrl);
                    Invoice tracked = await _context.Invoices.FirstAsync(i => i.Id == invoiceId);
                    tracked.CustomerEmail = request.Email;
                    try { await _context.SaveChangesAsync(); } catch { /* non-blocking */ }

                    return new SendInvoiceEmailResponse
                    {
                        Success  = true,
                        Provider = "SMTP",
                        Message  = $"Correo enviado correctamente a {request.Email} (vía servidor de correo propio).",
                        SentAt   = PeruClock.Now,
                    };
                }
                catch (Exception smtpEx)
                {
                    await SaveDeliveryLogAsync(invoice.Id, "EMAIL", "FAILED", request.Email, pdfUrl,
                        $"SMTP: {smtpEx.Message}");
                }
            }
            else
            {
                await SaveDeliveryLogAsync(invoice.Id, "EMAIL", "FAILED",
                    request.Email, invoice.PdfUrl, errorMsg);
            }

            return new SendInvoiceEmailResponse
            {
                Success          = false,
                RequiresFallback = pdfUrl is not null,
                PdfUrl           = pdfUrl,
                Message          = pdfUrl is not null
                    ? "Nubefact no pudo enviar el correo. Comparte el PDF manualmente usando el enlace."
                    : "Nubefact rechazó la solicitud de envío de correo.",
                Error = errorMsg
            };
        }

        // Update PDF URL if Nubefact returned a fresher one
        if (!string.IsNullOrWhiteSpace(result.Response.Url) && invoice.PdfUrl != result.Response.Url)
        {
            Invoice tracked = await _context.Invoices.FirstAsync(i => i.Id == invoiceId);
            tracked.PdfUrl = result.Response.Url;
        }

        // Persist the customer email on the invoice and optionally on the Customer entity
        Invoice trackedInvoice = await _context.Invoices.FirstAsync(i => i.Id == invoiceId);
        trackedInvoice.CustomerEmail = request.Email;

        if (request.SaveEmailToCustomer && invoice.Order?.CustomerId is int customerId)
        {
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
            if (customer is not null)
                customer.Email = request.Email;
        }

        await SaveDeliveryLogAsync(invoice.Id, "EMAIL", "SENT", request.Email, result.Response.Url);

        try { await _context.SaveChangesAsync(); } catch { /* non-blocking */ }

        return new SendInvoiceEmailResponse
        {
            Success = true,
            Message = $"Correo enviado correctamente a {request.Email} a través de Nubefact.",
            Provider = "NUBEFACT",
            SentAt = PeruClock.Now
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string NormalizePhone(string phone, string countryCode)
    {
        string digits = DigitsOnly.Replace(phone.Trim(), string.Empty);

        if (digits.Length == 0)
            throw new AppException("El número de WhatsApp no contiene dígitos válidos.", 422);

        // Remove leading zeros
        digits = digits.TrimStart('0');

        // Remove country prefix if already present
        string prefix = countryCode.TrimStart('0');
        if (digits.StartsWith(prefix) && digits.Length > prefix.Length)
            digits = digits[prefix.Length..];

        // Peruvian mobile numbers are 9 digits
        if (digits.Length < 7 || digits.Length > 15)
            throw new AppException($"El número de WhatsApp no es válido: {phone}", 422);

        return prefix + digits;
    }

    private static int MapDocTypeForNubefact(string? docType) => docType switch
    {
        "01" => 1,
        "03" => 2,
        "07" => 3,
        "08" => 4,
        _    => 1
    };

    private static string BuildEmailHtml(string customerName, string docLabel, string fullNumber, string pdfUrl)
    {
        return $"""
            <div style="font-family:Arial,sans-serif;font-size:14px;color:#111;max-width:520px;margin:0 auto">
              <h2 style="color:#f97316;margin-bottom:4px">HIITOP</h2>
              <p>Hola <strong>{customerName}</strong>,</p>
              <p>Te enviamos tu <strong>{docLabel} electrónica {fullNumber}</strong>.</p>
              <p>
                <a href="{pdfUrl}" style="display:inline-block;background:#f97316;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none;font-weight:700">
                  Descargar PDF
                </a>
              </p>
              <p style="color:#555">O copia este enlace en tu navegador:<br>
                <a href="{pdfUrl}" style="color:#f97316">{pdfUrl}</a>
              </p>
              <hr style="border:none;border-top:1px solid #eee;margin:20px 0">
              <p style="font-size:12px;color:#888">Gracias por tu compra. — Hiitop S.A.C.</p>
            </div>
            """;
    }

    private static string BuildWhatsAppMessage(
        string customerName, string docTypeLabel, string fullNumber, string pdfUrl)
    {
        return $"Hola {customerName}, te enviamos tu {docTypeLabel} electrónica {fullNumber}.\n\n" +
               $"Puedes descargarla aquí:\n{pdfUrl}\n\n" +
               "Gracias por tu compra.\n— Hiitop";
    }

    private async Task SaveDeliveryLogAsync(
        int invoiceId, string channel, string status,
        string? recipient, string? externalUrl, string? errorMessage = null)
    {
        try
        {
            _context.InvoiceDeliveryLogs.Add(new InvoiceDeliveryLog
            {
                InvoiceId = invoiceId,
                ChannelCode = channel,
                StatusCode = status,
                Recipient = recipient,
                ExternalUrl = externalUrl,
                ErrorMessage = errorMessage,
                CreatedAt = PeruClock.Now
            });
            await _context.SaveChangesAsync();
        }
        catch { /* logging must never break the main flow */ }
    }
}
