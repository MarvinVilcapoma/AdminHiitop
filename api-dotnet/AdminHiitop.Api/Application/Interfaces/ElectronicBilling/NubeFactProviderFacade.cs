using AdminHiitop.Api.Application.DTOs.ElectronicBilling;
using AdminHiitop.Api.Infrastructure.ElectronicBilling;

namespace AdminHiitop.Api.Application.Interfaces.ElectronicBilling;

public sealed class NubeFactProviderFacade : IElectronicBillingProvider
{
    private readonly NubeFactClient _client;

    public NubeFactProviderFacade(NubeFactClient client)
    {
        _client = client;
    }

    public string ProviderName => "NubeFact";

    public Task<bool> ValidateConfigurationAsync() =>
        _client.ValidateConfigurationAsync();

    public Task<NubeFactSubmitResult> SendDocumentAsync(NubeFactDocumentRequest request) =>
        _client.SendDocumentAsync(request);

    public Task<NubeFactSubmitResult> SendGuideDocumentAsync(NubeFactGuideDocumentRequest request) =>
        _client.SendGuideDocumentAsync(request);

    public Task<NubeFactSubmitResult> ConsultGuideAsync(NubeFactConsultGuideRequest request) =>
        _client.ConsultGuideAsync(request);

    public Task<NubeFactSubmitResult> SendDocumentByEmailAsync(NubeFactSendEmailRequest request) =>
        _client.SendDocumentByEmailAsync(request);

    public Task<NubeFactSubmitResult> SendBajaAsync(NubeFactBajaRequest request) =>
        _client.SendBajaAsync(request);
}
