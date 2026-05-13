namespace AdminHiitop.Api.Application.DTOs.Users;

public sealed class UserResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public IReadOnlyList<UserRoleResponse> Roles { get; set; } = Array.Empty<UserRoleResponse>();
}
