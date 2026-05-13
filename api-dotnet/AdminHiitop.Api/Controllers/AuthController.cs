using AdminHiitop.Api.Application.DTOs.Auth;
using AdminHiitop.Api.Application.DTOs.Common;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[Route("api")]
public sealed class AuthController : BaseApiController
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        AuthResponse response = await _authService.LoginAsync(request);
        return Ok(response);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        AuthResponse response = await _authService.RegisterAsync(request);
        return Ok(response);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        string? token = AuthHeaderHelper.ReadBearerToken(Request);
        await _authService.LogoutAsync(token);
        return Ok(new SuccessResponse());
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        string? token = AuthHeaderHelper.ReadBearerToken(Request);
        MeResponse response = await _authService.GetMeAsync(token);
        return Ok(response);
    }
}
