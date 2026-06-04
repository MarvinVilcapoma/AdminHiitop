using System.Text.Json;
using AdminHiitop.Api.Application.DTOs.ElectronicBilling;
using AdminHiitop.Api.Application.Helpers;
using AdminHiitop.Api.Application.Interfaces.ElectronicBilling;
using AdminHiitop.Api.Application.Interfaces.Repositories;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Invoices;

public sealed class InvoiceElectronicBillingService : IInvoiceElectronicBillingService
{
    private readonly IInvoiceElectronicBillingRepository _invoiceRepository;
    private readonly IElectronicBillingProvider _electronicBillingProvider;

    public InvoiceElectronicBillingService(
        IInvoiceElectronicBillingRepository invoiceRepository,
        IElectronicBillingProvider electronicBillingProvider)
    {
        _invoiceRepository = invoiceRepository;
        _electronicBillingProvider = electronicBillingProvider;
    }

    public Task<bool> TestConnectionAsync()
    {
        return _electronicBillingProvider.ValidateConfigurationAsync();
    }

    /// <summary>
    /// Emits a credit note (NC) via Nubefact using MapCreditNote — does NOT rely on Order items.
    /// </summary>
    public async Task<NubeFactSubmitResult> SendCreditNoteAsync(int creditNoteInvoiceId)
    {
        Invoice? invoice = await _invoiceRepository.GetInvoiceForSendAsync(creditNoteInvoiceId);
        if (invoice is null)
            throw new AppException("Nota de crédito no encontrada.", 404);

        NubeFactDocumentRequest request = NubeFactMappingHelper.MapCreditNote(invoice);
        NubeFactSubmitResult submitResult = await _electronicBillingProvider.SendDocumentAsync(request);

        ApplyResponse(invoice, submitResult);

        try { await _invoiceRepository.SaveChangesAsync(); }
        catch (DbUpdateException ex)
        {
            throw new AppException($"Error al actualizar la nota de crédito: {ex.InnerException?.Message ?? ex.Message}", 422);
        }

        try
        {
            await RegisterSendLogAsync(invoice, submitResult);
            await _invoiceRepository.SaveChangesAsync();
        }
        catch { /* non-blocking */ }

        return submitResult;
    }

    public async Task<NubeFactSubmitResult> SendInvoiceAsync(int invoiceId)
    {
        Invoice? invoice = await _invoiceRepository.GetInvoiceForSendAsync(invoiceId);

        if (invoice is null)
        {
            throw new AppException("Factura no encontrada.", 404);
        }

        NubeFactDocumentRequest request = NubeFactMappingHelper.MapInvoice(invoice);
        NubeFactSubmitResult submitResult = await _electronicBillingProvider.SendDocumentAsync(request);

        ApplyResponse(invoice, submitResult);

        // Save the invoice status update first — this is critical.
        try
        {
            await _invoiceRepository.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            string inner = ex.InnerException?.Message ?? ex.Message;
            throw new AppException($"Error al actualizar el comprobante tras el envio: {inner}", 422);
        }

        // Save the send log separately — failure here does NOT block the response.
        try
        {
            await RegisterSendLogAsync(invoice, submitResult);
            await _invoiceRepository.SaveChangesAsync();
        }
        catch { /* log in monitoring — do not surface to caller */ }

        return submitResult;
    }

    private async Task RegisterSendLogAsync(Invoice invoice, NubeFactSubmitResult submitResult)
    {
        SunatSendLog sendLog = new()
        {
            InvoiceId = invoice.Id,
            Environment = submitResult.Environment,
            Endpoint = submitResult.Endpoint,
            DocumentType = invoice.DocType,
            Serie = invoice.Serie,
            Correlativo = invoice.Correlativo,
            Status = submitResult.Success ? "success" : "error",
            ResponseCode = submitResult.Response.SunatResponseCode,
            ResponseDescription = submitResult.Response.SunatDescription,
            ErrorMessage = submitResult.Response.Errors,
            SentAt = PeruClock.Now
        };

        await _invoiceRepository.AddSendLogAsync(sendLog);
    }

    private static void ApplyResponse(Invoice invoice, NubeFactSubmitResult submitResult)
    {
        invoice.Status = submitResult.Success ? "sent" : "error";
        invoice.SunatDescription = submitResult.Response.SunatDescription ?? submitResult.Response.Errors;
        invoice.SunatNotesJson = submitResult.RawResponseJson;
        invoice.XmlContent = submitResult.Response.XmlZipBase64;
        invoice.CdrContent = submitResult.Response.CdrZipBase64;

        // Save public PDF URL: prefer enlace_del_pdf, fallback to enlace + ".pdf"
        string? pdfUrl = submitResult.Response.EnlaceDelPdf;
        if (string.IsNullOrWhiteSpace(pdfUrl) && !string.IsNullOrWhiteSpace(submitResult.Response.Url))
            pdfUrl = submitResult.Response.Url.TrimEnd('/') + ".pdf";

        if (!string.IsNullOrWhiteSpace(pdfUrl))
            invoice.PdfUrl = pdfUrl;

        if (int.TryParse(submitResult.Response.SunatResponseCode, out int responseCode))
        {
            invoice.SunatCode = responseCode;
        }

        if (!string.IsNullOrWhiteSpace(submitResult.Response.SunatNote))
        {
            invoice.Observations = submitResult.Response.SunatNote;
        }
    }
}
