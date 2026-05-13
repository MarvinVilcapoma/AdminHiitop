namespace AdminHiitop.Api.Application.DTOs.Auth;

public sealed class AuthUserResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public IReadOnlyList<AuthUserRoleResponse> Roles { get; set; } = Array.Empty<AuthUserRoleResponse>();
}
