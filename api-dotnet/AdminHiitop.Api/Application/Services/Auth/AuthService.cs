using AdminHiitop.Api.Application.DTOs.Auth;
using AdminHiitop.Api.Application.Helpers;
using AdminHiitop.Api.Application.Interfaces.Repositories;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Identity.Entities;
using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Infrastructure.Auth;
using AdminHiitop.Api.Shared.Exceptions;

namespace AdminHiitop.Api.Application.Services.Auth;

public sealed class AuthService : IAuthService
{
    private readonly IAuthRepository _authRepository;
    private readonly SessionTokenStore _sessionTokenStore;

    public AuthService(IAuthRepository authRepository, SessionTokenStore sessionTokenStore)
    {
        _authRepository = authRepository;
        _sessionTokenStore = sessionTokenStore;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        UserValidationHelper.ValidateLoginRequest(request);

        string normalizedEmail = UserValidationHelper.NormalizeEmail(request.Email);
        User? user = await _authRepository.GetByEmailAsync(normalizedEmail);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
        {
            throw new AppException("Credenciales inv\u00e1lidas.", 401);
        }

        if (!user.IsActive)
        {
            throw new AppException("El usuario est\u00e1 inactivo.", 403);
        }

        IReadOnlyList<Role> roles = await _authRepository.GetRolesByUserIdAsync(user.Id);
        IReadOnlyList<string> permissions = await _authRepository.GetPermissionNamesByUserIdAsync(user.Id);
        string token = _sessionTokenStore.Create(user.Id);

        return new AuthResponse
        {
            Message = "OK",
            Token = token,
            TokenType = "Bearer",
            Permissions = permissions,
            User = AuthMappingHelper.MapAuthUser(user, roles)
        };
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        UserValidationHelper.ValidateRegisterRequest(request);

        string normalizedEmail = UserValidationHelper.NormalizeEmail(request.Email);
        bool emailExists = await _authRepository.ExistsByEmailAsync(normalizedEmail, null);

        if (emailExists)
        {
            throw new AppException("Ya existe un usuario con ese correo.");
        }

        IReadOnlyList<int> normalizedRoleIds = request.RoleIds
            .Where(item => item > 0)
            .Distinct()
            .ToList();

        IReadOnlyList<Role> roles = await _authRepository.GetRolesByIdsAsync(normalizedRoleIds);

        if (roles.Count != normalizedRoleIds.Count)
        {
            throw new AppException("Uno o m\u00e1s roles no existen.");
        }

        User user = new()
        {
            Name = UserValidationHelper.NormalizeName(request.Name),
            Email = normalizedEmail,
            Password = BCrypt.Net.BCrypt.HashPassword(request.Password.Trim()),
            IsActive = true
        };

        await _authRepository.AddAsync(user);
        await _authRepository.SaveChangesAsync();

        if (normalizedRoleIds.Count > 0)
        {
            await _authRepository.ReplaceUserRolesAsync(user.Id, normalizedRoleIds);
            await _authRepository.SaveChangesAsync();
        }

        string token = _sessionTokenStore.Create(user.Id);
        IReadOnlyList<Role> assignedRoles = await _authRepository.GetRolesByUserIdAsync(user.Id);
        IReadOnlyList<string> permissions = await _authRepository.GetPermissionNamesByUserIdAsync(user.Id);

        return new AuthResponse
        {
            Message = "User registered",
            Token = token,
            TokenType = "Bearer",
            Permissions = permissions,
            User = AuthMappingHelper.MapAuthUser(user, assignedRoles)
        };
    }

    public Task LogoutAsync(string? token)
    {
        _sessionTokenStore.Remove(token);
        return Task.CompletedTask;
    }

    public async Task<MeResponse> GetMeAsync(string? token)
    {
        int? userId = _sessionTokenStore.GetUserId(token);

        if (!userId.HasValue)
        {
            throw new AppException("No autorizado.", 401);
        }

        User? user = await _authRepository.GetByIdAsync(userId.Value);

        if (user is null)
        {
            throw new AppException("Usuario no encontrado.", 404);
        }

        IReadOnlyList<Role> roles = await _authRepository.GetRolesByUserIdAsync(user.Id);
        IReadOnlyList<string> permissions = await _authRepository.GetPermissionNamesByUserIdAsync(user.Id);

        return new MeResponse
        {
            Permissions = permissions,
            User = AuthMappingHelper.MapAuthUser(user, roles)
        };
    }
}
