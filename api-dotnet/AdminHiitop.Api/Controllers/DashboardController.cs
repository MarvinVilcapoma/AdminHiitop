using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Application.DTOs.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[Route("api/dashboard")]
public sealed class DashboardController : BaseApiController
{
    private readonly IDashboardQueryService _dashboardQueryService;

    public DashboardController(IDashboardQueryService dashboardQueryService)
    {
        _dashboardQueryService = dashboardQueryService;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] DashboardSummaryFilterRequest request)
    {
        DashboardSummaryResponse response = await _dashboardQueryService.GetSummaryAsync(request);
        return Ok(response);
    }
}
