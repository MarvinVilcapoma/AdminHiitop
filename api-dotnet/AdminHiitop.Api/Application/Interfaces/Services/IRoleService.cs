using AdminHiitop.Api.Application.DTOs.Roles;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IRoleService
{
    Task<IReadOnlyList<RoleListItemResponse>> GetRolesAsync();
    Task<IReadOnlyList<PermissionResponse>> GetPermissionsAsync();
    Task<RoleDetailResponse> GetByIdAsync(int id);
    Task<RoleDetailResponse> CreateAsync(RoleUpsertRequest request);
    Task<RoleDetailResponse> UpdateAsync(int id, RoleUpsertRequest request);
    Task DeleteAsync(int id);
}
