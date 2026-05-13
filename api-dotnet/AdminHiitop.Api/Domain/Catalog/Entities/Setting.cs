namespace AdminHiitop.Api.Domain.Catalog.Entities;

public sealed class Setting
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? Label { get; set; }
    public string Type { get; set; } = "string";
    public string Group { get; set; } = "general";
}
