namespace AdminHiitop.Api.Shared.Results;

public sealed class ApiResult<T>
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public T? Data { get; init; }
    public IReadOnlyList<string>? Errors { get; init; }

    public static ApiResult<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResult<T> Fail(string message, IReadOnlyList<string>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors };
}
