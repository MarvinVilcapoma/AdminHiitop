namespace AdminHiitop.Api.Application.DTOs.Auth;

public sealed class AuthResponse
{
    public string Message { get; set; } = "OK";
    public string Token { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
    public AuthUserResponse User { get; set; } = new();
}
