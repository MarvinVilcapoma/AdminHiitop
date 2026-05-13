using AdminHiitop.Api.Domain.Identity.Entities;
using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Repositories;

public interface IUserRepository
{
    Task<IReadOnlyList<User>> GetUsersAsync(string? search);
    Task<User?> GetByIdAsync(int id);
    Task<bool> ExistsByEmailAsync(string email, int? excludedUserId = null);
    Task AddAsync(User user);
    Task DeleteAsync(User user);
    Task<IReadOnlyList<Role>> GetRolesLookupAsync();
    Task<IReadOnlyList<Role>> GetRolesByIdsAsync(IReadOnlyCollection<int> roleIds);
    Task<IReadOnlyList<Role>> GetRolesByUserIdAsync(int userId);
    Task<IReadOnlyDictionary<int, IReadOnlyList<Role>>> GetRolesMapByUserIdsAsync(IReadOnlyCollection<int> userIds);
    Task ReplaceRolesAsync(int userId, IReadOnlyCollection<int> roleIds);
    Task SaveChangesAsync();
}
