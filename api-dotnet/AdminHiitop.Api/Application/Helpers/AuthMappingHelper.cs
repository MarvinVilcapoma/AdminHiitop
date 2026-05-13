using AdminHiitop.Api.Application.DTOs.Auth;
using AdminHiitop.Api.Domain.Identity.Entities;
using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Application.Helpers;

public static class AuthMappingHelper
{
    public static AuthUserResponse MapAuthUser(User user, IReadOnlyList<Role> roles)
    {
        List<AuthUserRoleResponse> roleResponses = roles
            .OrderBy(item => item.Name)
            .Select(item => new AuthUserRoleResponse
            {
                Id = item.Id,
                Name = item.Name
            })
            .ToList();

        return new AuthUserResponse
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            IsActive = user.IsActive,
            Roles = roleResponses
        };
    }
}
