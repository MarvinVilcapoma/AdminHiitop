using AdminHiitop.Api.Application.DTOs.ElectronicBilling;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IInvoiceElectronicBillingService
{
    Task<bool> TestConnectionAsync();
    Task<NubeFactSubmitResult> SendInvoiceAsync(int invoiceId);
    Task<NubeFactSubmitResult> SendCreditNoteAsync(int creditNoteInvoiceId);
}
