namespace AdminHiitop.Api.Infrastructure.ElectronicBilling;

public sealed class NubeFactOptions
{
    public string Environment { get; set; } = "Demo";
    public string ApiUrl { get; set; } = string.Empty;
    public string DemoApiUrl { get; set; } = string.Empty;
    public string ProductionApiUrl { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public string OperationMode { get; set; } = "beta";
    public int TimeoutSeconds { get; set; } = 30;
    public int RetryAttempts { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 5;
}
