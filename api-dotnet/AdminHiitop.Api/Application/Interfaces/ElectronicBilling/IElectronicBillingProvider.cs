using AdminHiitop.Api.Application.DTOs.ElectronicBilling;

namespace AdminHiitop.Api.Application.Interfaces.ElectronicBilling;

public interface IElectronicBillingProvider
{
    string ProviderName { get; }
    Task<bool> ValidateConfigurationAsync();
    Task<NubeFactSubmitResult> SendDocumentAsync(NubeFactDocumentRequest request);
    Task<NubeFactSubmitResult> SendGuideDocumentAsync(NubeFactGuideDocumentRequest request);
    Task<NubeFactSubmitResult> ConsultGuideAsync(NubeFactConsultGuideRequest request);
    Task<NubeFactSubmitResult> SendDocumentByEmailAsync(NubeFactSendEmailRequest request);
    Task<NubeFactSubmitResult> SendBajaAsync(NubeFactBajaRequest request);
}
