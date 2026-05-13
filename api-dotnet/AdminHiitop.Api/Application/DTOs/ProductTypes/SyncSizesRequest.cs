namespace AdminHiitop.Api.Application.DTOs.ProductTypes;

public sealed class SyncSizesRequest
{
    public SizeRow[]? Sizes { get; init; }
}

public sealed class SizeRow
{
    public string Name { get; init; } = string.Empty;
    public int SortOrder { get; init; }
}
