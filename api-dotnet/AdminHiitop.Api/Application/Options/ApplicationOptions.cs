namespace AdminHiitop.Api.Application.Options;

public sealed class ApplicationOptions
{
    public const string SectionName = "Application";

    public string Name { get; set; } = "AdminHiitopV2";
    public string TimeZone { get; set; } = "America/Lima";
    public string Culture { get; set; } = "es-PE";
    public string Currency { get; set; } = "PEN";
}
