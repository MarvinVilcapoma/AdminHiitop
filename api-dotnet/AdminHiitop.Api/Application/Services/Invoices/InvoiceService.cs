using AdminHiitop.Api.Application.DTOs.ElectronicBilling;
using AdminHiitop.Api.Application.DTOs.Invoices;
using AdminHiitop.Api.Application.Helpers;
using AdminHiitop.Api.Application.Interfaces.ElectronicBilling;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using InvoiceSeriesEntity = AdminHiitop.Api.Domain.Sales.Entities.InvoiceSeries;

namespace AdminHiitop.Api.Application.Services.Invoices;

public sealed class InvoiceService : IInvoiceService
{
    private readonly AdminHiitopDbContext _context;
    private readonly IElectronicBillingProvider _billingProvider;
    private readonly IInvoiceElectronicBillingService _invoiceElectronicBillingService;
    private readonly IInvoiceSeriesService _seriesService;
    private readonly IHttpClientFactory _httpClientFactory;

    public InvoiceService(
        AdminHiitopDbContext context,
        IElectronicBillingProvider billingProvider,
        IInvoiceElectronicBillingService invoiceElectronicBillingService,
        IInvoiceSeriesService seriesService,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _billingProvider = billingProvider;
        _invoiceElectronicBillingService = invoiceElectronicBillingService;
        _seriesService = seriesService;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<object> GetAsync(int perPage, int page)
    {
        var query = _context.Invoices.AsNoTracking()
            .Include(item => item.Order)
            .Include(item => item.User)
            .OrderByDescending(item => item.IssuedAt)
            .ThenByDescending(item => item.Id);
        return await PaginationHelper.CreateAsync(query, page, perPage);
    }

    public async Task<object> GetSeriesAsync()
        => await _context.InvoiceSeries.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Serie).ToListAsync();

    public Task<Invoice?> GetByIdAsync(int id)
        => _context.Invoices.AsNoTracking().Include(item => item.Order).FirstOrDefaultAsync(item => item.Id == id);

    public async Task<object> CreateAsync(CreateInvoiceRequest request)
    {
        InvoiceSeriesEntity? seriesMeta;
        try
        {
            seriesMeta = await _context.InvoiceSeries
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == request.InvoiceSeriesId);
        }
        catch (Exception ex)
        {
            throw new AdminHiitop.Api.Shared.Exceptions.AppException(
                $"Error al cargar la serie de comprobante: {ex.InnerException?.Message ?? ex.Message}", 500);
        }

        if (seriesMeta is null)
            return new { error = true, message = "Serie no encontrada." };

        Order? order = request.OrderId.HasValue
            ? await _context.Orders.AsNoTracking().Include(item => item.Customer)
                .FirstOrDefaultAsync(item => item.Id == request.OrderId.Value)
            : null;

        // Guard: block if this order already has an active (non-cancelled) invoice.
        // Statuses that represent an in-flight or accepted invoice.
        if (request.OrderId.HasValue)
        {
            string[] activeStatuses = ["draft", "generated", "pending", "sent", "accepted", "accepted_with_obs",
                                       "processing", "ticket_generated", "pending_daily_summary", "daily_summary_sent"];
            bool hasActive = await _context.Invoices
                .AnyAsync(inv => inv.OrderId == request.OrderId.Value && activeStatuses.Contains(inv.Status));

            if (hasActive)
            {
                var existing = await _context.Invoices
                    .AsNoTracking()
                    .Where(inv => inv.OrderId == request.OrderId.Value && activeStatuses.Contains(inv.Status))
                    .OrderByDescending(inv => inv.Id)
                    .Select(inv => new { inv.FullNumber, inv.Status })
                    .FirstOrDefaultAsync();

                return new
                {
                    error   = true,
                    message = $"Este pedido ya tiene el comprobante {existing?.FullNumber} ({existing?.Status}). " +
                              "Anúlalo primero si necesitas emitir uno nuevo.",
                };
            }
        }

        // Atomic increment — never re-uses a number even under concurrent requests
        string serie;
        int correlativo;
        try
        {
            (serie, correlativo) = await _seriesService.GetNextAsync(request.InvoiceSeriesId);
        }
        catch (AdminHiitop.Api.Shared.Exceptions.AppException) { throw; }
        catch (Exception ex)
        {
            throw new AdminHiitop.Api.Shared.Exceptions.AppException(
                $"Error al reservar correlativo para la serie (id={request.InvoiceSeriesId}): {ex.Message}", 422);
        }

        decimal orderTotal = order?.Total ?? 0m;
        decimal orderIgv   = Math.Round(orderTotal * 0.18m / 1.18m, 2);
        decimal orderBase  = Math.Round(orderTotal - orderIgv, 2);

        var invoice = new Invoice
        {
            OrderId           = request.OrderId,
            InvoiceSeriesId   = seriesMeta.Id,
            DocType           = request.DocType ?? seriesMeta.DocType,
            Serie             = serie,
            Correlativo       = correlativo,
            FullNumber        = $"{serie}-{correlativo:00000000}",
            Status            = "draft",
            CustomerDocType   = request.CustomerDocType,
            CustomerDocNumber = request.CustomerDocNumber,
            CustomerName      = request.CustomerName ?? order?.CustomerName,
            // Capture contact info at emission time for later WhatsApp / email delivery
            CustomerPhone     = order?.Customer?.Phone,
            CustomerEmail     = order?.CustomerEmail ?? order?.Customer?.Email,
            Currency          = "PEN",
            FormOfPayment     = request.FormOfPayment ?? "contado",
            PaymentMethodId   = request.PaymentMethodId,
            MtoOperGravadas   = orderBase,
            MtoIgv            = orderIgv,
            ValorVenta        = orderBase,
            SubTotal          = orderBase,
            MtoImpVenta       = orderTotal,
            IssuedAt          = PeruClock.Now,
            SunatDescription  = "Documento generado en borrador."
        };

        _context.Invoices.Add(invoice);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
        {
            string inner = ex.InnerException?.Message ?? ex.Message;
            // Surface duplicate full_number as a user-facing error, not a generic 500
            throw new AdminHiitop.Api.Shared.Exceptions.AppException(
                inner.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) || inner.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                    ? $"Ya existe un comprobante con el numero {invoice.FullNumber}. Verifica el correlativo de la serie."
                    : $"Error al guardar el comprobante: {inner}",
                422);
        }

        if (!request.AutoSend)
        {
            return new
            {
                invoice,
                sunat_result = new { success = true, code = 0, description = invoice.SunatDescription }
            };
        }

        // Invoice is saved — now attempt auto-send. Any exception (network, DNS, etc.)
        // is surfaced as a readable message instead of a generic 500.
        AdminHiitop.Api.Application.DTOs.ElectronicBilling.NubeFactSubmitResult? sendResult = null;
        string? sendError = null;
        try
        {
            sendResult = await _invoiceElectronicBillingService.SendInvoiceAsync(invoice.Id);
        }
        catch (AdminHiitop.Api.Shared.Exceptions.AppException ex) { sendError = ex.Message; }
        catch (Exception ex) { sendError = ex.InnerException?.Message ?? ex.Message; }

        var updated = await _context.Invoices.AsNoTracking().FirstAsync(i => i.Id == invoice.Id);
        return new
        {
            invoice = updated,
            sunat_result = sendResult is null
                ? new { success = false, code = 0, description = sendError ?? "No se pudo enviar a Nubefact.", errors = sendError, url = (string?)null, accepted = (bool?)null, provider = "NubeFact", environment = "", result = (object?)null }
                : (object)new
                {
                    success     = sendResult.Success,
                    code        = 0,
                    description = sendResult.Response.SunatDescription ?? sendResult.Response.Errors,
                    errors      = sendResult.Response.Errors,
                    url         = sendResult.Response.Url,
                    accepted    = sendResult.Response.AceptadaPorSunat,
                    provider    = sendResult.ProviderName,
                    environment = sendResult.Environment,
                    result      = sendResult.Response
                }
        };
    }

    public async Task<object> TestConnectionAsync()
        => new
        {
            success = await _invoiceElectronicBillingService.TestConnectionAsync(),
            provider = _billingProvider.ProviderName
        };

    public async Task<object> SendAsync(int id)
    {
        try
        {
            var result = await _invoiceElectronicBillingService.SendInvoiceAsync(id);
            return new
            {
                success = result.Success,
                provider = result.ProviderName,
                environment = result.Environment,
                endpoint = result.Endpoint,
                result = result.Response
            };
        }
        catch (AdminHiitop.Api.Shared.Exceptions.AppException) { throw; }
        catch (Exception ex)
        {
            throw new AdminHiitop.Api.Shared.Exceptions.AppException(
                $"Error al enviar a Nubefact: {ex.InnerException?.Message ?? ex.Message}", 500);
        }
    }

    public async Task<object> VoidAsync(int id, VoidInvoiceRequest request)
    {
        Invoice? original = await _context.Invoices
            .Include(i => i.Order)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (original is null)
            return new { error = true, message = "Comprobante no encontrado." };

        if (original.Status == "cancelled")
            return new { error = true, message = "Este comprobante ya fue anulado." };

        if (!new[] { "sent", "accepted", "accepted_with_obs" }.Contains(original.Status))
            return new { error = true, message = $"Solo se pueden anular comprobantes enviados o aceptados. Estado actual: {original.Status}." };

        // Decide method
        double daysSince = (PeruClock.Now - original.IssuedAt).TotalDays;
        bool withinSevenDays = daysSince <= 7;

        string method = request.VoidMethod switch
        {
            "baja"        => "baja",
            "credit_note" => "credit_note",
            _             => withinSevenDays ? "baja" : "credit_note",  // auto
        };

        if (method == "baja")
        {
            if (!withinSevenDays)
                return new { error = true, message = "No se puede comunicar la baja: han pasado más de 7 días desde la emisión. Usa una Nota de Crédito." };

            return await SendBajaToNubefactAsync(original, request.Motivo ?? "Anulación de comprobante");
        }

        // Mark original as cancelled
        original.Status = "cancelled";
        original.SunatDescription = "Comprobante anulado mediante nota de crédito.";

        if (!request.AutoSend)
        {
            await _context.SaveChangesAsync();
            return new { success = true, message = "Comprobante anulado localmente. Nota de crédito pendiente de emisión." };
        }

        // Determine NC document type: 07 = NC-Factura, 07 = NC-Boleta (same code in Nubefact format)
        // NC series: look for FC01 (for Factura) or BC01 (for Boleta)
        string expectedSeriesPrefix = original.DocType == "01" ? "FC" : "BC";
        InvoiceSeriesEntity? ncSeries = await _context.InvoiceSeries
            .AsNoTracking()
            .Where(s => s.IsActive && s.DocType == "07" && s.Serie.StartsWith(expectedSeriesPrefix))
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync();

        if (ncSeries is null)
        {
            await _context.SaveChangesAsync();
            return new
            {
                success = false,
                error = true,
                message = $"No se encontró serie activa de Nota de Crédito para {(original.DocType == "01" ? "Factura" : "Boleta")}. Crea una serie '{expectedSeriesPrefix}C01' en Configuración > Parámetros fiscales."
            };
        }

        string ncSerie;
        int ncCorrelativo;
        try { (ncSerie, ncCorrelativo) = await _seriesService.GetNextAsync(ncSeries.Id); }
        catch (AdminHiitop.Api.Shared.Exceptions.AppException ex)
        {
            await _context.SaveChangesAsync();
            return new { success = false, error = true, message = ex.Message };
        }

        var creditNote = new Invoice
        {
            OrderId           = original.OrderId,
            InvoiceSeriesId   = ncSeries.Id,
            DocType           = "07",
            Serie             = ncSerie,
            Correlativo       = ncCorrelativo,
            FullNumber        = $"{ncSerie}-{ncCorrelativo:00000000}",
            Status            = "draft",
            CustomerDocType   = original.CustomerDocType,
            CustomerDocNumber = original.CustomerDocNumber,
            CustomerName      = original.CustomerName,
            CustomerPhone     = original.CustomerPhone,
            CustomerEmail     = original.CustomerEmail,
            Currency          = original.Currency,
            FormOfPayment     = original.FormOfPayment,
            MtoOperGravadas   = original.MtoOperGravadas,
            MtoIgv            = original.MtoIgv,
            ValorVenta        = original.ValorVenta,
            SubTotal          = original.SubTotal,
            MtoImpVenta       = original.MtoImpVenta,
            IssuedAt          = PeruClock.Now,
            NoteMotive        = request.NoteMotive,
            NoteMotiveDesc    = request.NoteMotiveDesc ?? GetNoteMotiveDescription(request.NoteMotive),
            RefDocType        = original.DocType,
            RefDocNumber      = original.FullNumber,
            RefDocDate        = original.IssuedAt,
            SunatDescription  = "Nota de crédito generada en borrador."
        };

        _context.Invoices.Add(creditNote);
        await _context.SaveChangesAsync();

        // Emit via Nubefact
        AdminHiitop.Api.Application.DTOs.ElectronicBilling.NubeFactSubmitResult? sendResult = null;
        string? sendError = null;
        try
        {
            sendResult = await _invoiceElectronicBillingService.SendCreditNoteAsync(creditNote.Id);
        }
        catch (AdminHiitop.Api.Shared.Exceptions.AppException ex) { sendError = ex.Message; }
        catch (Exception ex) { sendError = ex.InnerException?.Message ?? ex.Message; }

        var updatedNc = await _context.Invoices.AsNoTracking().FirstAsync(i => i.Id == creditNote.Id);
        return new
        {
            success = sendResult?.Success ?? false,
            message = sendResult?.Success == true
                ? "Nota de crédito emitida y aceptada por SUNAT."
                : (sendError ?? "No se pudo enviar la nota de crédito a Nubefact."),
            credit_note = updatedNc,
            original_invoice_id = original.Id,
            sunat_result = sendResult is null
                ? new { success = false, description = sendError }
                : (object)new
                {
                    success     = sendResult.Success,
                    description = sendResult.Response.SunatDescription ?? sendResult.Response.Errors,
                    url         = sendResult.Response.Url,
                    accepted    = sendResult.Response.AceptadaPorSunat
                }
        };
    }

    private async Task<object> SendBajaToNubefactAsync(Invoice invoice, string motivo)
    {
        int tipo = invoice.DocType == "01" ? 1 : 2;
        string fechaEmision = invoice.IssuedAt.ToString("dd/MM/yyyy");

        var bajaRequest = new AdminHiitop.Api.Application.DTOs.ElectronicBilling.NubeFactBajaRequest
        {
            TipoDeComprobante = tipo,
            Serie             = invoice.Serie,
            Correlativo       = invoice.Correlativo,
            FechaDeEmision    = fechaEmision,
            Motivo            = motivo,
        };

        NubeFactSubmitResult result;
        try { result = await _billingProvider.SendBajaAsync(bajaRequest); }
        catch (Exception ex)
        {
            return new { success = false, error = true, void_method = "baja",
                message = $"Error al comunicar la baja: {ex.InnerException?.Message ?? ex.Message}" };
        }

        if (result.Success)
        {
            invoice.Status = "cancelled";
            invoice.SunatDescription = "Comprobante anulado mediante comunicación de baja.";
            await _context.SaveChangesAsync();
        }

        return new
        {
            success      = result.Success,
            void_method  = "baja",
            message      = result.Success
                ? $"Comunicación de baja enviada correctamente. {invoice.FullNumber} anulado en SUNAT."
                : $"Nubefact no pudo procesar la baja: {result.Response.Errors ?? result.Response.SunatDescription}",
            sunat_result = new
            {
                success     = result.Success,
                description = result.Response.SunatDescription ?? result.Response.Errors,
            },
            invoice_id   = invoice.Id,
        };
    }

    public async Task<object> GetVoidCheckAsync(int id)
    {
        Invoice? invoice = await _context.Invoices.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice is null)
            return new { error = true, message = "Comprobante no encontrado." };

        double daysSince = (PeruClock.Now - invoice.IssuedAt).TotalDays;
        bool withinSevenDays = daysSince <= 7;
        bool canVoid = new[] { "sent", "accepted", "accepted_with_obs" }.Contains(invoice.Status);
        bool isNoteDoc = new[] { "07", "08" }.Contains(invoice.DocType);

        return new
        {
            invoice_id       = id,
            full_number      = invoice.FullNumber,
            status           = invoice.Status,
            issued_at        = invoice.IssuedAt,
            days_since       = (int)daysSince,
            within_seven_days = withinSevenDays,
            can_void         = canVoid && !isNoteDoc,
            can_use_baja     = canVoid && withinSevenDays && !isNoteDoc,
            can_use_credit_note = canVoid && !isNoteDoc,
            recommendation   = withinSevenDays
                ? "Puedes usar Comunicación de Baja (más limpio) o Nota de Crédito."
                : "Han pasado más de 7 días. Solo puedes emitir una Nota de Crédito.",
        };
    }

    private static string GetNoteMotiveDescription(string code) => code switch
    {
        "01" => "Anulación de la operación",
        "02" => "Anulación por error en RUC",
        "03" => "Corrección por error en descripción",
        "04" => "Descuento global",
        "05" => "Descuento por ítem",
        "06" => "Devolución total",
        "07" => "Devolución por ítem",
        "13" => "Ajuste en operaciones de exportación",
        _ => "Nota de crédito"
    };

    public async Task<InvoiceFileContent?> GetXmlAsync(int id)
    {
        Invoice? entity = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
        if (entity is null) return null;
        byte[]? content = NubeFactStorageHelper.DecodeBase64(entity.XmlContent);
        return content is null ? null : new InvoiceFileContent(content, $"{entity.FullNumber}.xml.zip");
    }

    public async Task<InvoiceFileContent?> GetCdrAsync(int id)
    {
        Invoice? entity = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
        if (entity is null) return null;
        byte[]? content = NubeFactStorageHelper.DecodeBase64(entity.CdrContent);
        return content is null ? null : new InvoiceFileContent(content, $"{entity.FullNumber}.cdr.zip");
    }

    public async Task<InvoiceFileContent?> GetPdfAsync(int id)
    {
        Invoice? entity = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
        if (entity is null) return null;

        var response = NubeFactStorageHelper.ReadStoredResponse(entity.SunatNotesJson);
        byte[]? content = NubeFactStorageHelper.DecodeBase64(response?.PdfZipBase64);

        if (content is not null)
            return new InvoiceFileContent(content, $"{entity.FullNumber}.pdf.zip");

        // No base64 stored — download from Nubefact server-side to avoid CORS issues.
        string? pdfUrl = entity.PdfUrl
            ?? response?.EnlaceDelPdf
            ?? (response?.Url is not null ? response.Url.TrimEnd('/') + ".pdf" : null);

        if (string.IsNullOrWhiteSpace(pdfUrl)) return null;

        using HttpClient http = _httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(30);
        byte[] pdfBytes = await http.GetByteArrayAsync(pdfUrl);
        return new InvoiceFileContent(pdfBytes, $"{entity.FullNumber}.pdf");
    }
}
