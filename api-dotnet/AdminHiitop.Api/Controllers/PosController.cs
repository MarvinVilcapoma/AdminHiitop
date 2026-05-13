using AdminHiitop.Api.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[Route("api/pos")]
public sealed class PosController : BaseApiController
{
    private readonly IPosService _posService;

    public PosController(IPosService posService)
    {
        _posService = posService;
    }

    [HttpGet("initial-data")]
    public async Task<IActionResult> InitialData()
    {
        return Ok(await _posService.GetInitialDataAsync());
    }
}
