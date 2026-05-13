using AdminHiitop.Api.Domain.Common;

namespace AdminHiitop.Api.Domain.Identity.Entities;

public sealed class Permission : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string GuardName { get; set; } = "api";

    public ICollection<RoleHasPermission> RolePermissions { get; set; } = new List<RoleHasPermission>();
}
