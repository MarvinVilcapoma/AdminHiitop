namespace AdminHiitop.Api.Application.DTOs.Settings;

public sealed class UpdateSettingRequest
{
    public string? Value { get; init; }
    public string? Label { get; init; }
    public string? Type { get; init; }
    public string? Group { get; init; }
}
