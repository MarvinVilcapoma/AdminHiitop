using AdminHiitop.Api.Application.DTOs.Roles;
using AdminHiitop.Api.Application.Helpers;
using AdminHiitop.Api.Application.Interfaces.Repositories;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Identity.Entities;
using AdminHiitop.Api.Shared.Exceptions;

namespace AdminHiitop.Api.Application.Services.Roles;

public sealed class RoleService : IRoleService
{
    private readonly IRoleRepository _roleRepository;

    public RoleService(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    public async Task<IReadOnlyList<RoleListItemResponse>> GetRolesAsync()
    {
        IReadOnlyList<Role> roles = await _roleRepository.GetRolesAsync();
        return roles.Select(IdentityMappingHelper.MapRoleListItem).ToList();
    }

    public async Task<IReadOnlyList<PermissionResponse>> GetPermissionsAsync()
    {
        IReadOnlyList<Permission> permissions = await _roleRepository.GetPermissionsAsync();
        return permissions.Select(IdentityMappingHelper.MapPermission).ToList();
    }

    public async Task<RoleDetailResponse> GetByIdAsync(int id)
    {
        Role role = await FindRoleAsync(id);
        return IdentityMappingHelper.MapRoleDetail(role);
    }

    public async Task<RoleDetailResponse> CreateAsync(RoleUpsertRequest request)
    {
        UserValidationHelper.ValidateRoleUpsertRequest(request);
        string normalizedName = request.Name.Trim();

        bool exists = await _roleRepository.ExistsByNameAsync(normalizedName, null);
        if (exists)
        {
            throw new AppException("Ya existe un rol con ese nombre.");
        }

        IReadOnlyList<int> permissionIds = await ResolvePermissionIdsAsync(normalizedName, request.PermissionIds);

        Role role = new()
        {
            Name = normalizedName,
            GuardName = "api"
        };

        await _roleRepository.AddAsync(role);
        await _roleRepository.SaveChangesAsync();

        await _roleRepository.ReplacePermissionsAsync(role.Id, permissionIds);
        await _roleRepository.SaveChangesAsync();

        Role refreshedRole = await FindRoleForReadAsync(role.Id);
        return IdentityMappingHelper.MapRoleDetail(refreshedRole);
    }

    public async Task<RoleDetailResponse> UpdateAsync(int id, RoleUpsertRequest request)
    {
        UserValidationHelper.ValidateRoleUpsertRequest(request);

        Role role = await FindRoleAsync(id);
        string normalizedName = request.Name.Trim();

        bool exists = await _roleRepository.ExistsByNameAsync(normalizedName, id);
        if (exists)
        {
            throw new AppException("Ya existe un rol con ese nombre.");
        }

        IReadOnlyList<int> permissionIds = await ResolvePermissionIdsAsync(normalizedName, request.PermissionIds);

        role.Name = normalizedName;
        await _roleRepository.ReplacePermissionsAsync(role.Id, permissionIds);
        await _roleRepository.SaveChangesAsync();

        Role refreshedRole = await FindRoleForReadAsync(role.Id);
        return IdentityMappingHelper.MapRoleDetail(refreshedRole);
    }

    public async Task DeleteAsync(int id)
    {
        Role role = await FindRoleAsync(id);
        await _roleRepository.DeleteAsync(role);
        await _roleRepository.SaveChangesAsync();
    }

    private async Task<Role> FindRoleAsync(int id)
    {
        Role? role = await _roleRepository.GetByIdAsync(id);

        if (role is null)
        {
            throw new AppException("Rol no encontrado.", 404);
        }

        return role;
    }

    private async Task<Role> FindRoleForReadAsync(int id)
    {
        Role? role = await _roleRepository.GetByIdForReadAsync(id);

        if (role is null)
        {
            throw new AppException("Rol no encontrado.", 404);
        }

        return role;
    }

    private async Task<IReadOnlyList<int>> ResolvePermissionIdsAsync(string roleName, IEnumerable<int> requestedPermissionIds)
    {
        if (string.Equals(roleName, "admin", StringComparison.OrdinalIgnoreCase))
        {
            IReadOnlyList<Permission> allPermissions = await _roleRepository.GetPermissionsAsync();
            return allPermissions.Select(item => item.Id).ToList();
        }

        IReadOnlyList<int> permissionIds = requestedPermissionIds
            .Where(item => item > 0)
            .Distinct()
            .ToList();

        IReadOnlyList<Permission> permissions = await _roleRepository.GetPermissionsByIdsAsync(permissionIds);

        if (permissions.Count != permissionIds.Count)
        {
            throw new AppException("Uno o m\u00e1s permisos no existen.");
        }

        return permissionIds;
    }
}
