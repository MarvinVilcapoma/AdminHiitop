using AdminHiitop.Api.Application.DTOs.Users;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IUserService
{
    Task<IReadOnlyList<UserResponse>> GetUsersAsync(string? search);
    Task<UserResponse> GetByIdAsync(int id);
    Task<IReadOnlyList<UserRoleLookupResponse>> GetRolesLookupAsync();
    Task<UserResponse> CreateAsync(UserUpsertRequest request);
    Task<UserResponse> UpdateAsync(int id, UserUpsertRequest request);
    Task DeleteAsync(int id);
}
