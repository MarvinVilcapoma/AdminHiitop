using System.Text.Json;
using AdminHiitop.Api.Application.DTOs.ElectronicBilling;
using AdminHiitop.Api.Application.Helpers;
using AdminHiitop.Api.Application.Interfaces.ElectronicBilling;
using AdminHiitop.Api.Application.Interfaces.Repositories;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Shared.Exceptions;

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
        await RegisterSendLogAsync(invoice, submitResult);
        await _invoiceRepository.SaveChangesAsync();

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
            SentAt = DateTime.UtcNow
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
