using AdminHiitop.Api.Application.DTOs.Roles;
using AdminHiitop.Api.Application.DTOs.Users;
using AdminHiitop.Api.Domain.Identity.Entities;
using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Application.Helpers;

public static class IdentityMappingHelper
{
    public static UserResponse MapUser(User user, IReadOnlyList<Role> roles)
    {
        List<UserRoleResponse> roleResponses = roles
            .OrderBy(item => item.Name)
            .Select(item => new UserRoleResponse
            {
                Id = item.Id,
                Name = item.Name
            })
            .ToList();

        return new UserResponse
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            Roles = roleResponses
        };
    }

    public static UserRoleLookupResponse MapRoleLookup(Role role)
    {
        return new UserRoleLookupResponse
        {
            Id = role.Id,
            Name = role.Name
        };
    }

    public static RoleListItemResponse MapRoleListItem(Role role)
    {
        return new RoleListItemResponse
        {
            Id = role.Id,
            Name = role.Name,
            GuardName = role.GuardName,
            PermissionsCount = role.RolePermissions.Count
        };
    }

    public static RoleDetailResponse MapRoleDetail(Role role)
    {
        List<PermissionResponse> permissions = role.RolePermissions
            .Select(item => item.Permission)
            .OrderBy(item => item.Name)
            .Select(item => new PermissionResponse
            {
                Id = item.Id,
                Name = item.Name,
                GuardName = item.GuardName
            })
            .ToList();

        return new RoleDetailResponse
        {
            Id = role.Id,
            Name = role.Name,
            GuardName = role.GuardName,
            Permissions = permissions
        };
    }

    public static PermissionResponse MapPermission(Permission permission)
    {
        return new PermissionResponse
        {
            Id = permission.Id,
            Name = permission.Name,
            GuardName = permission.GuardName
        };
    }
}
