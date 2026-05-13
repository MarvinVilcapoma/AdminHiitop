namespace AdminHiitop.Api.Application.DTOs.Common;

public sealed class SuccessResponse
{
    public bool Success { get; set; } = true;
    public string? Message { get; set; }
}
