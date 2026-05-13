namespace AdminHiitop.Api.Application.DTOs.Stocks;

public sealed class StockColorReferenceResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? HexCode { get; set; }
}
