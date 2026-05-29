namespace AdminHiitop.Api.Infrastructure.ElectronicBilling;

public sealed class NubeFactOptions
{
    public string Environment     { get; set; } = "Production";
    public string ApiUrl          { get; set; } = string.Empty;
    public string ProductionApiUrl { get; set; } = string.Empty;
    public string ApiToken        { get; set; } = string.Empty;
    public int    TimeoutSeconds  { get; set; } = 30;
}
