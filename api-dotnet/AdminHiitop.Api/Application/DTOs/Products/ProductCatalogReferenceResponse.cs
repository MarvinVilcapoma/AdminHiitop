namespace AdminHiitop.Api.Application.DTOs.Products;

public sealed class ProductCatalogReferenceResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<ProductSizeResponse> Sizes { get; set; } = Array.Empty<ProductSizeResponse>();
}

public sealed class ProductSizeResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
