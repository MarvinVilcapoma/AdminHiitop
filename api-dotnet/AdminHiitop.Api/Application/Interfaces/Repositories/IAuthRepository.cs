using AdminHiitop.Api.Domain.Identity.Entities;
using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Repositories;

public interface IAuthRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdAsync(int userId);
    Task<bool> ExistsByEmailAsync(string email, int? excludedUserId = null);
    Task AddAsync(User user);
    Task<IReadOnlyList<Role>> GetRolesByUserIdAsync(int userId);
    Task<IReadOnlyList<Role>> GetRolesByIdsAsync(IReadOnlyCollection<int> roleIds);
    Task<IReadOnlyList<string>> GetPermissionNamesByUserIdAsync(int userId);
    Task ReplaceUserRolesAsync(int userId, IReadOnlyCollection<int> roleIds);
    Task SaveChangesAsync();
}
