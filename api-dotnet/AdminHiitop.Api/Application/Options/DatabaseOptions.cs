namespace AdminHiitop.Api.Application.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string Provider { get; set; } = "SqlServer";
    public bool AutoMigrate { get; set; } = true;
    public bool AutoSeed { get; set; } = true;
    public int CommandTimeoutSeconds { get; set; } = 60;
    public bool EnableSensitiveDataLogging { get; set; } = false;
}
