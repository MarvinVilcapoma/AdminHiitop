using AdminHiitop.Api.Application.DTOs.Invoices;
using AdminHiitop.Api.Application.Helpers;
using AdminHiitop.Api.Application.Interfaces.ElectronicBilling;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Invoices;

public sealed class InvoiceService : IInvoiceService
{
    private readonly AdminHiitopDbContext _context;
    private readonly IElectronicBillingProvider _billingProvider;
    private readonly IInvoiceElectronicBillingService _invoiceElectronicBillingService;

    public InvoiceService(
        AdminHiitopDbContext context,
        IElectronicBillingProvider billingProvider,
        IInvoiceElectronicBillingService invoiceElectronicBillingService)
    {
        _context = context;
        _billingProvider = billingProvider;
        _invoiceElectronicBillingService = invoiceElectronicBillingService;
    }

    public async Task<object> GetAsync(int perPage, int page, CancellationToken cancellationToken)
    {
        var query = _context.Invoices.AsNoTracking()
            .Include(item => item.Order)
            .Include(item => item.User)
            .OrderByDescending(item => item.IssuedAt)
            .ThenByDescending(item => item.Id);
        return await PaginationHelper.CreateAsync(query, page, perPage, cancellationToken);
    }

    public async Task<object> GetSeriesAsync(CancellationToken cancellationToken)
        => await _context.InvoiceSeries.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Serie).ToListAsync(cancellationToken);

    public Task<Invoice?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => _context.Invoices.AsNoTracking().Include(item => item.Order).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<object> CreateAsync(CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        var series = await _context.InvoiceSeries.FirstOrDefaultAsync(item => item.Id == request.InvoiceSeriesId, cancellationToken);
        if (series is null) return new { error = true, message = "Serie no encontrada." };

        Order? order = request.OrderId.HasValue
            ? await _context.Orders.Include(item => item.Customer).FirstOrDefaultAsync(item => item.Id == request.OrderId.Value, cancellationToken)
            : null;

        int correlativo = series.NextNumber;
        series.NextNumber += 1;

        var invoice = new Invoice
        {
            OrderId = request.OrderId,
            InvoiceSeriesId = series.Id,
            DocType = request.DocType ?? series.DocType,
            Serie = series.Serie,
            Correlativo = correlativo,
            FullNumber = $"{series.Serie}-{correlativo:00000000}",
            Status = request.AutoSend ? "sent" : "draft",
            CustomerDocType = request.CustomerDocType,
            CustomerDocNumber = request.CustomerDocNumber,
            CustomerName = request.CustomerName ?? order?.CustomerName,
            Currency = "PEN",
            FormOfPayment = request.FormOfPayment ?? "contado",
            PaymentMethodId = request.PaymentMethodId,
            MtoOperGravadas = order?.Total ?? 0,
            MtoIgv = Math.Round((order?.Total ?? 0) * 0.18m / 1.18m, 2),
            ValorVenta = order?.Total ?? 0,
            SubTotal = order?.Total ?? 0,
            MtoImpVenta = order?.Total ?? 0,
            IssuedAt = DateTime.UtcNow,
            SunatDescription = request.AutoSend ? "Documento enviado al proveedor configurado." : "Documento generado en borrador."
        };

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(cancellationToken);

        return new
        {
            invoice,
            sunat_result = new { success = true, code = 0, description = invoice.SunatDescription }
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

    public async Task<object> VoidAsync(int id, CancellationToken cancellationToken)
    {
        Invoice? entity = await _context.Invoices.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return new { error = true, message = "Comprobante no encontrado." };
        entity.Status = "cancelled";
        entity.SunatDescription = "Comprobante anulado.";
        await _context.SaveChangesAsync(cancellationToken);
        return new { success = true, message = entity.SunatDescription };
    }

    public async Task<InvoiceFileContent?> GetXmlAsync(int id, CancellationToken cancellationToken)
    {
        Invoice? entity = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return null;
        byte[]? content = NubeFactStorageHelper.DecodeBase64(entity.XmlContent);
        return content is null ? null : new InvoiceFileContent(content, $"{entity.FullNumber}.xml.zip");
    }

    public async Task<InvoiceFileContent?> GetCdrAsync(int id, CancellationToken cancellationToken)
    {
        Invoice? entity = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return null;
        byte[]? content = NubeFactStorageHelper.DecodeBase64(entity.CdrContent);
        return content is null ? null : new InvoiceFileContent(content, $"{entity.FullNumber}.cdr.zip");
    }

    public async Task<InvoiceFileContent?> GetPdfAsync(int id, CancellationToken cancellationToken)
    {
        Invoice? entity = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return null;
        var response = NubeFactStorageHelper.ReadStoredResponse(entity.SunatNotesJson);
        byte[]? content = NubeFactStorageHelper.DecodeBase64(response?.PdfZipBase64);
        return content is null ? null : new InvoiceFileContent(content, $"{entity.FullNumber}.pdf.zip");
    }
}
