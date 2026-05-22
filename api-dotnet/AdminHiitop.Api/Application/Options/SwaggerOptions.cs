namespace AdminHiitop.Api.Application.Options;

public sealed class SwaggerOptions
{
    public const string SectionName = "Swagger";

    public bool Enabled { get; set; } = true;
    public string Title { get; set; } = "AdminHiitopV2 API";
    public string Version { get; set; } = "v1";
}
