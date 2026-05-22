using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Application.DTOs.Pos;
using AdminHiitop.Api.Infrastructure.Auth;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[Route("api/pos")]
public sealed class PosController : BaseApiController
{
    private readonly IPosService _posService;
    private readonly SessionTokenStore _sessionTokenStore;

    public PosController(IPosService posService, SessionTokenStore sessionTokenStore)
    {
        _posService = posService;
        _sessionTokenStore = sessionTokenStore;
    }

    [HttpGet("initial-data")]
    public async Task<IActionResult> InitialData()
    {
        return Ok(await _posService.GetInitialDataAsync());
    }

    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder([FromBody] PosOrderCreateRequest request)
    {
        string? token = AuthHeaderHelper.ReadBearerToken(Request);
        int? userId = _sessionTokenStore.GetUserId(token);
        return Ok(await _posService.CreateOrderAsync(request, userId));
    }
}
