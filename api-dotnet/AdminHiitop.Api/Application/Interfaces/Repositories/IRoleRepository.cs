using AdminHiitop.Api.Domain.Identity.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Repositories;

public interface IRoleRepository
{
    Task<IReadOnlyList<Role>> GetRolesAsync();
    Task<Role?> GetByIdAsync(int id);
    Task<Role?> GetByIdForReadAsync(int id);
    Task<bool> ExistsByNameAsync(string name, int? excludedRoleId = null);
    Task AddAsync(Role role);
    Task DeleteAsync(Role role);
    Task<IReadOnlyList<Permission>> GetPermissionsAsync();
    Task<IReadOnlyList<Permission>> GetPermissionsByIdsAsync(IReadOnlyCollection<int> permissionIds);
    Task ReplacePermissionsAsync(int roleId, IReadOnlyCollection<int> permissionIds);
    Task SaveChangesAsync();
}
