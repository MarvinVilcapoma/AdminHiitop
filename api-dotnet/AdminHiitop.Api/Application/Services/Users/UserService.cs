using AdminHiitop.Api.Application.DTOs.Users;
using AdminHiitop.Api.Application.Helpers;
using AdminHiitop.Api.Application.Interfaces.Repositories;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Identity.Entities;
using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Shared.Exceptions;

namespace AdminHiitop.Api.Application.Services.Users;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<IReadOnlyList<UserResponse>> GetUsersAsync(string? search)
    {
        IReadOnlyList<User> users = await _userRepository.GetUsersAsync(search);
        IReadOnlyList<int> userIds = users.Select(item => item.Id).ToList();
        IReadOnlyDictionary<int, IReadOnlyList<Role>> rolesMap = await _userRepository.GetRolesMapByUserIdsAsync(userIds);

        List<UserResponse> responses = new(users.Count);

        foreach (User user in users)
        {
            IReadOnlyList<Role> roles = rolesMap.TryGetValue(user.Id, out IReadOnlyList<Role>? userRoles)
                ? userRoles
                : Array.Empty<Role>();

            responses.Add(IdentityMappingHelper.MapUser(user, roles));
        }

        return responses;
    }

    public async Task<UserResponse> GetByIdAsync(int id)
    {
        User user = await FindUserAsync(id);
        IReadOnlyList<Role> roles = await _userRepository.GetRolesByUserIdAsync(id);
        return IdentityMappingHelper.MapUser(user, roles);
    }

    public async Task<IReadOnlyList<UserRoleLookupResponse>> GetRolesLookupAsync()
    {
        IReadOnlyList<Role> roles = await _userRepository.GetRolesLookupAsync();
        return roles.Select(IdentityMappingHelper.MapRoleLookup).ToList();
    }

    public async Task<UserResponse> CreateAsync(UserUpsertRequest request)
    {
        UserValidationHelper.ValidateUserUpsertRequest(request, true);

        string normalizedEmail = UserValidationHelper.NormalizeEmail(request.Email);
        bool emailExists = await _userRepository.ExistsByEmailAsync(normalizedEmail, null);

        if (emailExists)
        {
            throw new AppException("Ya existe un usuario con ese correo.");
        }

        IReadOnlyList<int> roleIds = request.RoleIds.Where(item => item > 0).Distinct().ToList();
        await EnsureRolesExistAsync(roleIds);

        User user = new()
        {
            Name = UserValidationHelper.NormalizeName(request.Name),
            Email = normalizedEmail,
            Password = BCrypt.Net.BCrypt.HashPassword(request.Password!.Trim()),
            IsActive = request.IsActive
        };

        await _userRepository.AddAsync(user);
        await _userRepository.SaveChangesAsync();

        await _userRepository.ReplaceRolesAsync(user.Id, roleIds);
        await _userRepository.SaveChangesAsync();

        IReadOnlyList<Role> assignedRoles = await _userRepository.GetRolesByUserIdAsync(user.Id);
        return IdentityMappingHelper.MapUser(user, assignedRoles);
    }

    public async Task<UserResponse> UpdateAsync(int id, UserUpsertRequest request)
    {
        UserValidationHelper.ValidateUserUpsertRequest(request, false);

        User user = await FindUserAsync(id);
        string normalizedEmail = UserValidationHelper.NormalizeEmail(request.Email);
        bool emailExists = await _userRepository.ExistsByEmailAsync(normalizedEmail, id);

        if (emailExists)
        {
            throw new AppException("Ya existe un usuario con ese correo.");
        }

        IReadOnlyList<int> roleIds = request.RoleIds.Where(item => item > 0).Distinct().ToList();
        await EnsureRolesExistAsync(roleIds);

        user.Name = UserValidationHelper.NormalizeName(request.Name);
        user.Email = normalizedEmail;
        user.IsActive = request.IsActive;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.Password = BCrypt.Net.BCrypt.HashPassword(request.Password.Trim());
        }

        await _userRepository.ReplaceRolesAsync(user.Id, roleIds);
        await _userRepository.SaveChangesAsync();

        IReadOnlyList<Role> assignedRoles = await _userRepository.GetRolesByUserIdAsync(user.Id);
        return IdentityMappingHelper.MapUser(user, assignedRoles);
    }

    public async Task DeleteAsync(int id)
    {
        User user = await FindUserAsync(id);
        await _userRepository.DeleteAsync(user);
        await _userRepository.SaveChangesAsync();
    }

    private async Task<User> FindUserAsync(int id)
    {
        User? user = await _userRepository.GetByIdAsync(id);

        if (user is null)
        {
            throw new AppException("Usuario no encontrado.", 404);
        }

        return user;
    }

    private async Task EnsureRolesExistAsync(IReadOnlyList<int> roleIds)
    {
        IReadOnlyList<Role> roles = await _userRepository.GetRolesByIdsAsync(roleIds);

        if (roles.Count != roleIds.Count)
        {
            throw new AppException("Uno o m\u00e1s roles no existen.");
        }
    }
}
