namespace AdminHiitop.Api.Application.DTOs.Products;

public sealed class ProductColorResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? HexCode { get; set; }
}
