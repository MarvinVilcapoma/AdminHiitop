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

    public InvoiceService(
        AdminHiitopDbContext context,
        IElectronicBillingProvider billingProvider,
        IInvoiceElectronicBillingService invoiceElectronicBillingService,
        IInvoiceSeriesService seriesService)
    {
        _context = context;
        _billingProvider = billingProvider;
        _invoiceElectronicBillingService = invoiceElectronicBillingService;
        _seriesService = seriesService;
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

        var sendResult = await _invoiceElectronicBillingService.SendInvoiceAsync(invoice.Id);
        var updated = await _context.Invoices.AsNoTracking().FirstAsync(i => i.Id == invoice.Id);
        return new
        {
            invoice = updated,
            sunat_result = new
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

    public async Task<object> VoidAsync(int id)
    {
        Invoice? entity = await _context.Invoices.FirstOrDefaultAsync(item => item.Id == id);
        if (entity is null) return new { error = true, message = "Comprobante no encontrado." };
        entity.Status = "cancelled";
        entity.SunatDescription = "Comprobante anulado.";
        await _context.SaveChangesAsync();
        return new { success = true, message = entity.SunatDescription };
    }

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
        return content is null ? null : new InvoiceFileContent(content, $"{entity.FullNumber}.pdf.zip");
    }
}
