namespace AdminHiitop.Api.Application.DTOs.Roles;

public sealed class RoleDetailResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string GuardName { get; set; } = string.Empty;
    public IReadOnlyList<PermissionResponse> Permissions { get; set; } = Array.Empty<PermissionResponse>();
}
