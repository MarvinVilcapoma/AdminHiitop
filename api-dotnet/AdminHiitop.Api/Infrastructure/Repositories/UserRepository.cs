using AdminHiitop.Api.Application.Interfaces.Repositories;
using AdminHiitop.Api.Domain.Identity.Entities;
using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly AdminHiitopDbContext _context;

    public UserRepository(AdminHiitopDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<User>> GetUsersAsync(string? search)
    {
        IQueryable<User> query = _context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string normalizedSearch = search.Trim();
            query = query.Where(item => item.Name.Contains(normalizedSearch) || item.Email.Contains(normalizedSearch));
        }

        return await query
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync();
    }

    public Task<User?> GetByIdAsync(int id)
    {
        return _context.Users.FirstOrDefaultAsync(item => item.Id == id);
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

    public Task DeleteAsync(User user)
    {
        _context.Users.Remove(user);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Role>> GetRolesLookupAsync()
    {
        return await _context.Roles
            .AsNoTracking()
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
            .OrderBy(item => item.Name)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Role>> GetRolesByUserIdAsync(int userId)
    {
        return await _context.ModelHasRoles
            .AsNoTracking()
            .Where(item => item.ModelId == userId && item.ModelType == ModelHasRole.UserModelType)
            .Include(item => item.Role)
            .Select(item => item.Role)
            .OrderBy(item => item.Name)
            .ToListAsync();
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlyList<Role>>> GetRolesMapByUserIdsAsync(IReadOnlyCollection<int> userIds)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<int, IReadOnlyList<Role>>();
        }

        List<ModelHasRole> assignments = await _context.ModelHasRoles
            .AsNoTracking()
            .Where(item => item.ModelType == ModelHasRole.UserModelType && userIds.Contains(item.ModelId))
            .Include(item => item.Role)
            .ToListAsync();

        Dictionary<int, IReadOnlyList<Role>> rolesMap = assignments
            .GroupBy(item => item.ModelId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Role>)group
                    .Select(item => item.Role)
                    .OrderBy(item => item.Name)
                    .ToList());

        return rolesMap;
    }

    public async Task ReplaceRolesAsync(int userId, IReadOnlyCollection<int> roleIds)
    {
        List<ModelHasRole> currentAssignments = await _context.ModelHasRoles
            .Where(item => item.ModelId == userId && item.ModelType == ModelHasRole.UserModelType)
            .ToListAsync();

        if (currentAssignments.Count > 0)
        {
            _context.ModelHasRoles.RemoveRange(currentAssignments);
        }

        if (roleIds.Count == 0)
        {
            return;
        }

        List<ModelHasRole> newAssignments = roleIds
            .Distinct()
            .Select(item => new ModelHasRole
            {
                RoleId = item,
                ModelId = userId,
                ModelType = ModelHasRole.UserModelType
            })
            .ToList();

        await _context.ModelHasRoles.AddRangeAsync(newAssignments);
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}
