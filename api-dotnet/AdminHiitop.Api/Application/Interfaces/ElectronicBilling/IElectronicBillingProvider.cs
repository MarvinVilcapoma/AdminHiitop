using AdminHiitop.Api.Application.DTOs.ElectronicBilling;

namespace AdminHiitop.Api.Application.Interfaces.ElectronicBilling;

public interface IElectronicBillingProvider
{
    string ProviderName { get; }
    Task<bool> ValidateConfigurationAsync();
    Task<NubeFactSubmitResult> SendDocumentAsync(NubeFactDocumentRequest request);
}
