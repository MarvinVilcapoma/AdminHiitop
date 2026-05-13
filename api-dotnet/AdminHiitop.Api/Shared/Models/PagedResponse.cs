namespace AdminHiitop.Api.Shared.Models;

public sealed class PagedResponse<T>
{
    public IReadOnlyList<T> Data { get; init; } = Array.Empty<T>();
    public int CurrentPage { get; init; }
    public int LastPage { get; init; }
    public int PerPage { get; init; }
    public int Total { get; init; }
}
