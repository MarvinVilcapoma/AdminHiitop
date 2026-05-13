namespace AdminHiitop.Api.Application.DTOs.SaleImports;

public sealed class ImportRowsRequest
{
    public List<Dictionary<string, object?>>? Rows { get; init; }
}
