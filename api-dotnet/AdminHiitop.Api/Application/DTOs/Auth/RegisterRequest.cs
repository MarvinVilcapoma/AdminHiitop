namespace AdminHiitop.Api.Application.DTOs.Auth;

public sealed class RegisterRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? ConfirmPassword { get; set; }
    public List<int> RoleIds { get; set; } = new();
}
