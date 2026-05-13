using AdminHiitop.Api.Application.Interfaces.Repositories;
using AdminHiitop.Api.Domain.Identity.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Infrastructure.Repositories;

public sealed class RoleRepository : IRoleRepository
{
    private readonly AdminHiitopDbContext _context;

    public RoleRepository(AdminHiitopDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Role>> GetRolesAsync()
    {
        return await _context.Roles
            .AsNoTracking()
            .Include(item => item.RolePermissions)
            .OrderBy(item => item.Name)
            .ToListAsync();
    }

    public Task<Role?> GetByIdAsync(int id)
    {
        return _context.Roles
            .Include(item => item.RolePermissions)
            .ThenInclude(item => item.Permission)
            .FirstOrDefaultAsync(item => item.Id == id);
    }

    public Task<Role?> GetByIdForReadAsync(int id)
    {
        return _context.Roles
            .AsNoTracking()
            .Include(item => item.RolePermissions)
            .ThenInclude(item => item.Permission)
            .FirstOrDefaultAsync(item => item.Id == id);
    }

    public Task<bool> ExistsByNameAsync(string name, int? excludedRoleId = null)
    {
        return _context.Roles.AnyAsync(
            item => item.Name == name && (!excludedRoleId.HasValue || item.Id != excludedRoleId.Value));
    }

    public Task AddAsync(Role role)
    {
        return _context.Roles.AddAsync(role).AsTask();
    }

    public async Task DeleteAsync(Role role)
    {
        List<RoleHasPermission> permissions = await _context.RoleHasPermissions
            .Where(item => item.RoleId == role.Id)
            .ToListAsync();

        if (permissions.Count > 0)
        {
            _context.RoleHasPermissions.RemoveRange(permissions);
        }

        List<ModelHasRole> assignments = await _context.ModelHasRoles
            .Where(item => item.RoleId == role.Id)
            .ToListAsync();

        if (assignments.Count > 0)
        {
            _context.ModelHasRoles.RemoveRange(assignments);
        }

        _context.Roles.Remove(role);
    }

    public async Task<IReadOnlyList<Permission>> GetPermissionsAsync()
    {
        return await _context.Permissions
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Permission>> GetPermissionsByIdsAsync(IReadOnlyCollection<int> permissionIds)
    {
        if (permissionIds.Count == 0)
        {
            return Array.Empty<Permission>();
        }

        return await _context.Permissions
            .AsNoTracking()
            .Where(item => permissionIds.Contains(item.Id))
            .ToListAsync();
    }

    public async Task ReplacePermissionsAsync(int roleId, IReadOnlyCollection<int> permissionIds)
    {
        List<RoleHasPermission> currentPermissions = await _context.RoleHasPermissions
            .Where(item => item.RoleId == roleId)
            .ToListAsync();

        if (currentPermissions.Count > 0)
        {
            _context.RoleHasPermissions.RemoveRange(currentPermissions);
        }

        if (permissionIds.Count == 0)
        {
            return;
        }

        List<RoleHasPermission> newPermissions = permissionIds
            .Distinct()
            .Select(item => new RoleHasPermission
            {
                RoleId = roleId,
                PermissionId = item
            })
            .ToList();

        await _context.RoleHasPermissions.AddRangeAsync(newPermissions);
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}
