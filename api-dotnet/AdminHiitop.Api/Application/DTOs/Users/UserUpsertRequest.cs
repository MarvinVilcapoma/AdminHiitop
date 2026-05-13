namespace AdminHiitop.Api.Application.DTOs.Users;

public sealed class UserUpsertRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Password { get; set; }
    public bool IsActive { get; set; } = true;
    public List<int> RoleIds { get; set; } = new();
}
