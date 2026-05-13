using AdminHiitop.Api.Application.DTOs.Auth;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task LogoutAsync(string? token);
    Task<MeResponse> GetMeAsync(string? token);
}
