using System.Text.RegularExpressions;
using AdminHiitop.Api.Application.DTOs.Common;
using AdminHiitop.Api.Application.DTOs.ElectronicBilling;
using AdminHiitop.Api.Application.Helpers;
using AdminHiitop.Api.Application.Interfaces.ElectronicBilling;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.OrderGuides;

public sealed class OrderGuideService(
    AdminHiitopDbContext context,
    IElectronicBillingProvider billingProvider,
    IInvoiceSeriesService seriesService,
    IHttpClientFactory httpClientFactory,
    IEmailService emailService) : IOrderGuideService
{
    private readonly AdminHiitopDbContext _context = context;
    private readonly IElectronicBillingProvider _billingProvider = billingProvider;
    private readonly IInvoiceSeriesService _seriesService = seriesService;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IEmailService _emailService = emailService;

    private static readonly Regex DigitsOnly = new(@"\D", RegexOptions.Compiled);

    // DocType → serie mapping  (must match seeded invoice_series rows)
    private static readonly Dictionary<string, string> SeriesByDocType = new()
    {
        ["09"] = "TTT1",  // GRE Remitente
        ["31"] = "VVV1",  // GRE Transportista
    };

    public async Task<IReadOnlyList<Order>> GetGuidesAsync()
    {
        return await _context.Orders
            .AsNoTracking()
            .Include(o => o.DocumentType)
            .Include(o => o.OrderStatus)
            .Include(o => o.Customer)
            .Where(item =>
                item.GuideSeries != null ||
                item.GuideStatus != null ||
                (item.DocumentType != null &&
                 (item.DocumentType.Code == "GUIA_REMISION" ||
                  item.DocumentType.Code == "GUIA_REMISION_TRANSP")))
            .OrderByDescending(item => item.OrderDate)
            .ThenByDescending(item => item.Id)
            .ToListAsync();
    }

    public Task<Order?> GetByOrderIdAsync(int orderId)
    {
        return _context.Orders
            .AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(item => item.Id == orderId);
    }

    public async Task<object?> SendAsync(int orderId)
    {
        Order? order = await _context.Orders
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(item => item.Id == orderId);

        if (order is null) return null;

        // Determine guide type: "09" = Remitente (default), "31" = Transportista
        string guideDocType = order.GuideType ?? "09";
        string serie = SeriesByDocType.GetValueOrDefault(guideDocType, "TTT1");

        // Atomic increment from centralized series table
        (string resolvedSerie, int correlativo) = await _seriesService.GetNextAsync(guideDocType, serie);

        order.GuideType        = guideDocType;
        order.GuideSeries      = resolvedSerie;
        order.GuideCorrelativo = correlativo;
        order.GuideFullNumber  = $"{resolvedSerie}-{correlativo:00000000}";
        order.GuideSentAt      = PeruClock.Now;
        order.GuideStatus      = "draft";
        order.GuidePdfLink     = null;
        order.GuideConsultedAt = null;

        NubeFactGuideDocumentRequest guideRequest = NubeFactMappingHelper.MapGuide(order, resolvedSerie, correlativo, guideDocType);
        NubeFactSubmitResult result = await _billingProvider.SendGuideDocumentAsync(guideRequest);

        order.GuideStatus           = result.Success ? "draft" : "error";   // NubeFact always returns false initially
        order.GuideSunatCode        = int.TryParse(result.Response.SunatResponseCode, out int code) ? code : null;
        order.GuideSunatDescription = result.Response.SunatDescription ?? result.Response.Errors;
        order.GuideXmlContent       = result.Response.XmlZipBase64;
        order.GuideCdrContent       = result.Response.CdrZipBase64;

        // PDF link if immediately available (demo mode may provide it)
        if (!string.IsNullOrWhiteSpace(result.Response.EnlaceDelPdf))
            order.GuidePdfLink = result.Response.EnlaceDelPdf;

        await _context.SaveChangesAsync();

        return new
        {
            success     = result.Success,
            provider    = result.ProviderName,
            environment = result.Environment,
            order = new
            {
                order.Id,
                order.GuideFullNumber,
                order.GuideStatus,
                order.GuideSunatDescription,
                order.GuidePdfLink,
            },
            result = result.Response,
        };
    }

    /// <summary>
    /// Step 2: Consult NubeFact for the acceptance status and PDF/CDR download links.
    /// Should be called after SendAsync until aceptada_por_sunat = true.
    /// </summary>
    public async Task<object?> ConsultAsync(int orderId)
    {
        Order? order = await _context.Orders
            .FirstOrDefaultAsync(item => item.Id == orderId);

        if (order is null) return null;
        if (string.IsNullOrWhiteSpace(order.GuideSeries) || order.GuideCorrelativo is null)
            return null;

        string guideDocType = order.GuideType ?? "09";
        // NubeFact tipo: 7 = Remitente, 8 = Transportista
        int nubefactTipo = guideDocType == "31" ? 8 : 7;

        var consultRequest = new NubeFactConsultGuideRequest
        {
            TipoDeComprobante = nubefactTipo,
            Serie             = order.GuideSeries,
            Numero            = order.GuideCorrelativo.Value.ToString(),
        };

        NubeFactSubmitResult result = await _billingProvider.ConsultGuideAsync(consultRequest);

        order.GuideConsultedAt = PeruClock.Now;

        bool accepted = result.Response.AceptadaPorSunat == true;
        if (accepted)
        {
            order.GuideStatus  = "accepted";
            order.GuidePdfLink = result.Response.EnlaceDelPdf;
            order.GuideXmlContent = result.Response.XmlZipBase64 ?? order.GuideXmlContent;
            order.GuideCdrContent = result.Response.CdrZipBase64 ?? order.GuideCdrContent;
        }
        else if (!string.IsNullOrWhiteSpace(result.Response.SunatDescription))
        {
            order.GuideStatus = "rejected";
            order.GuideSunatDescription = result.Response.SunatDescription;
        }

        await _context.SaveChangesAsync();

        return new
        {
            accepted,
            order = new
            {
                order.Id,
                order.GuideFullNumber,
                order.GuideStatus,
                order.GuideSunatDescription,
                order.GuidePdfLink,
                consulted_at = order.GuideConsultedAt,
            },
            result = result.Response,
        };
    }

    public async Task<FileDownloadResponse?> GetXmlAsync(int orderId)
    {
        Order? order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == orderId);

        if (order is null || string.IsNullOrWhiteSpace(order.GuideXmlContent))
            return null;

        byte[]? content = NubeFactStorageHelper.DecodeBase64(order.GuideXmlContent);
        if (content is null) return null;

        return new FileDownloadResponse
        {
            Content     = content,
            ContentType = "application/octet-stream",
            FileName    = $"{order.GuideFullNumber}.xml.zip"
        };
    }

    public async Task<FileDownloadResponse?> GetCdrAsync(int orderId)
    {
        Order? order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == orderId);

        if (order is null || string.IsNullOrWhiteSpace(order.GuideCdrContent))
            return null;

        byte[]? content = NubeFactStorageHelper.DecodeBase64(order.GuideCdrContent);
        if (content is null) return null;

        return new FileDownloadResponse
        {
            Content     = content,
            ContentType = "application/octet-stream",
            FileName    = $"{order.GuideFullNumber}.cdr.zip"
        };
    }

    public async Task<FileDownloadResponse?> GetPdfAsync(int orderId)
    {
        Order? order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order is null || string.IsNullOrWhiteSpace(order.GuidePdfLink))
            return null;

        using HttpClient client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        byte[] content = await client.GetByteArrayAsync(order.GuidePdfLink);

        return new FileDownloadResponse
        {
            Content     = content,
            ContentType = "application/pdf",
            FileName    = $"{order.GuideFullNumber ?? $"GUIA-{orderId}"}.pdf"
        };
    }

    public async Task<object> GetWhatsAppLinkAsync(int orderId, string phone, string countryCode = "51")
    {
        Order? order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new AppException("Pedido no encontrado.", 404);

        if (string.IsNullOrWhiteSpace(phone))
            throw new AppException("El número de WhatsApp es obligatorio.", 422);

        if (string.IsNullOrWhiteSpace(order.GuidePdfLink))
            throw new AppException("La guía aún no tiene PDF disponible. Emite y consulta el estado primero.", 422);

        string normalizedPhone = NormalizePhone(phone, countryCode);

        string guideNum = order.GuideFullNumber ?? $"GUIA-{orderId}";
        string recipientName = order.GuideRecipientName ?? order.CustomerName ?? "cliente";

        string message = $"Hola {recipientName}, te enviamos tu Guía de Remisión Electrónica {guideNum}.\n\n" +
                         $"Puedes descargarla aquí:\n{order.GuidePdfLink}\n\n" +
                         "Gracias.\n— Hiitop";

        string whatsAppUrl = $"https://wa.me/{normalizedPhone}?text={Uri.EscapeDataString(message)}";

        return new { whatsAppUrl, phoneFormatted = normalizedPhone };
    }

    public async Task<object> SendEmailAsync(int orderId, string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new AppException("El correo electrónico no es válido.", 422);

        Order? order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new AppException("Pedido no encontrado.", 404);

        if (string.IsNullOrWhiteSpace(order.GuidePdfLink))
            throw new AppException("La guía aún no tiene PDF disponible. Consulta el estado en SUNAT primero.", 422);

        if (!_emailService.IsConfigured)
        {
            return new
            {
                success          = false,
                requires_fallback = true,
                pdf_url          = order.GuidePdfLink,
                message          = "SMTP no configurado. Comparte el PDF manualmente usando el enlace.",
            };
        }

        string guideNum   = order.GuideFullNumber ?? $"GUIA-{orderId}";
        string recipient  = order.GuideRecipientName ?? order.CustomerName ?? "destinatario";
        string subject    = $"Guía de Remisión Electrónica {guideNum} — Hiitop";
        string body       = $"""
            <div style="font-family:Arial,sans-serif;font-size:14px;color:#111;max-width:520px;margin:0 auto">
              <h2 style="color:#f97316;margin-bottom:4px">HIITOP S.A.C.</h2>
              <p>Hola <strong>{recipient}</strong>,</p>
              <p>Te enviamos tu <strong>Guía de Remisión Electrónica {guideNum}</strong>.</p>
              <p>
                <a href="{order.GuidePdfLink}" style="display:inline-block;background:#f97316;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none;font-weight:700">
                  Descargar PDF
                </a>
              </p>
              <p style="color:#555">O copia este enlace:<br>
                <a href="{order.GuidePdfLink}" style="color:#f97316">{order.GuidePdfLink}</a>
              </p>
              <hr style="border:none;border-top:1px solid #eee;margin:20px 0">
              <p style="font-size:12px;color:#888">— Hiitop S.A.C.</p>
            </div>
            """;

        await _emailService.SendAsync(email, subject, body);

        return new
        {
            success = true,
            message = $"Correo enviado correctamente a {email}.",
            guide_number = guideNum,
        };
    }

    private static string NormalizePhone(string phone, string countryCode)
    {
        string digits = DigitsOnly.Replace(phone.Trim(), string.Empty).TrimStart('0');
        string prefix = countryCode.TrimStart('0');
        if (digits.StartsWith(prefix) && digits.Length > prefix.Length)
            digits = digits[prefix.Length..];
        if (digits.Length < 7 || digits.Length > 15)
            throw new AppException($"El número de WhatsApp no es válido: {phone}", 422);
        return prefix + digits;
    }
}
