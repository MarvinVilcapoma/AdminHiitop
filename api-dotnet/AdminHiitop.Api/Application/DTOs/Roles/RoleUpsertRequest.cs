namespace AdminHiitop.Api.Application.DTOs.Roles;

public sealed class RoleUpsertRequest
{
    public string Name { get; set; } = string.Empty;
    public List<int> PermissionIds { get; set; } = new();
}
