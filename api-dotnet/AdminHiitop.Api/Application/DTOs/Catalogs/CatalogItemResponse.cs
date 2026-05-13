namespace AdminHiitop.Api.Application.DTOs.Catalogs;

public sealed class CatalogItemResponse
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}
