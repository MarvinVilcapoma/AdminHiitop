namespace AdminHiitop.Api.Application.DTOs.Roles;

public sealed class RoleListItemResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string GuardName { get; set; } = string.Empty;
    public int PermissionsCount { get; set; }
}
