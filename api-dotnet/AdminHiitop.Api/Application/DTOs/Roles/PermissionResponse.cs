namespace AdminHiitop.Api.Application.DTOs.Roles;

public sealed class PermissionResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string GuardName { get; set; } = string.Empty;
}
