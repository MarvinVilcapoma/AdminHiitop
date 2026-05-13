namespace AdminHiitop.Api.Application.DTOs.Products;

public sealed class ProductQueryRequest
{
    public int? PerPage { get; set; }
    public int Page { get; set; } = 1;
    public string? Search { get; set; }
}
