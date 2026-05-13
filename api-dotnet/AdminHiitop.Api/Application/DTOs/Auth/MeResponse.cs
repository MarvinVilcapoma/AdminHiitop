namespace AdminHiitop.Api.Application.DTOs.Auth;

public sealed class MeResponse
{
    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
    public AuthUserResponse User { get; set; } = new();
}
