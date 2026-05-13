using AdminHiitop.Api.Application.Interfaces.Repositories;
using AdminHiitop.Api.Domain.Identity.Entities;
using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Infrastructure.Repositories;

public sealed class AuthRepository : IAuthRepository
{
    private readonly AdminHiitopDbContext _context;

    public AuthRepository(AdminHiitopDbContext context)
    {
        _context = context;
    }

    public Task<User?> GetByEmailAsync(string email)
    {
        return _context.Users.FirstOrDefaultAsync(item => item.Email == email);
    }

    public Task<User?> GetByIdAsync(int userId)
    {
        return _context.Users.FirstOrDefaultAsync(item => item.Id == userId);
    }

    public Task<bool> ExistsByEmailAsync(string email, int? excludedUserId = null)
    {
        return _context.Users.AnyAsync(
            item => item.Email == email && (!excludedUserId.HasValue || item.Id != excludedUserId.Value));
    }

    public Task AddAsync(User user)
    {
        return _context.Users.AddAsync(user).AsTask();
    }

    public async Task<IReadOnlyList<Role>> GetRolesByUserIdAsync(int userId)
    {
        return await _context.ModelHasRoles
            .AsNoTracking()
            .Where(item => item.ModelId == userId && item.ModelType == ModelHasRole.UserModelType)
            .Select(item => item.Role)
            .IgnoreQueryFilters()
            .OrderBy(item => item.Name)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Role>> GetRolesByIdsAsync(IReadOnlyCollection<int> roleIds)
    {
        if (roleIds.Count == 0)
        {
            return Array.Empty<Role>();
        }

        return await _context.Roles
            .AsNoTracking()
            .Where(item => roleIds.Contains(item.Id))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<string>> GetPermissionNamesByUserIdAsync(int userId)
    {
        int[] roleIds = await _context.ModelHasRoles
            .AsNoTracking()
            .Where(item => item.ModelId == userId && item.ModelType == ModelHasRole.UserModelType)
            .Select(item => item.RoleId)
            .Distinct()
            .ToArrayAsync();

        if (roleIds.Length == 0)
        {
            return Array.Empty<string>();
        }

        return await _context.RoleHasPermissions
            .AsNoTracking()
            .Where(item => roleIds.Contains(item.RoleId))
            .Select(item => item.Permission.Name)
            .Distinct()
            .OrderBy(item => item)
            .ToListAsync();
    }

    public async Task ReplaceUserRolesAsync(int userId, IReadOnlyCollection<int> roleIds)
    {
        List<ModelHasRole> currentRoles = await _context.ModelHasRoles
            .Where(item => item.ModelId == userId && item.ModelType == ModelHasRole.UserModelType)
            .ToListAsync();

        if (currentRoles.Count > 0)
        {
            _context.ModelHasRoles.RemoveRange(currentRoles);
        }

        if (roleIds.Count == 0)
        {
            return;
        }

        List<ModelHasRole> newRoles = roleIds
            .Distinct()
            .Select(item => new ModelHasRole
            {
                RoleId = item,
                ModelId = userId,
                ModelType = ModelHasRole.UserModelType
            })
            .ToList();

        await _context.ModelHasRoles.AddRangeAsync(newRoles);
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}
