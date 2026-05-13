namespace AdminHiitop.Api.Application.DTOs.Settings;

public sealed class SettingResponse
{
    public string Key { get; init; } = string.Empty;
    public string? Value { get; init; }
    public string? Label { get; init; }
    public string Type { get; init; } = "string";
    public string Group { get; init; } = "general";
}
