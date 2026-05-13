namespace AdminHiitop.Api.Application.DTOs.ElectronicBilling;

public sealed class NubeFactSubmitResult
{
    public bool Success { get; set; }
    public string ProviderName { get; set; } = "NubeFact";
    public string Environment { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public NubeFactDocumentRequest Request { get; set; } = new();
    public NubeFactDocumentResponse Response { get; set; } = new();
    public string RawRequestJson { get; set; } = string.Empty;
    public string RawResponseJson { get; set; } = string.Empty;
}
