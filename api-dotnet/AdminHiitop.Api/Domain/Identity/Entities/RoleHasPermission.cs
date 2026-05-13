namespace AdminHiitop.Api.Domain.Identity.Entities;

public sealed class RoleHasPermission
{
    public int PermissionId { get; set; }
    public int RoleId { get; set; }

    public Permission Permission { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
