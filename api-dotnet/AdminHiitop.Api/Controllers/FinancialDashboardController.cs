using AdminHiitop.Api.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[Route("api/financial-dashboard")]
public sealed class FinancialDashboardController : BaseApiController
{
    private readonly IFinancialDashboardService _service;

    public FinancialDashboardController(IFinancialDashboardService service)
    {
        _service = service;
    }

    /// <summary>Full dashboard for a given month: totals, charts, recent movements.</summary>
    [HttpGet("monthly-summary")]
    public async Task<IActionResult> MonthlySummary([FromQuery] int year, [FromQuery] int month)
    {
        if (year < 2000 || year > 2100 || month < 1 || month > 12)
            return BadRequest("Año o mes inválido.");

        return Ok(await _service.GetMonthlySummaryAsync(year, month));
    }

    /// <summary>12-month income/expense series for a year.</summary>
    [HttpGet("yearly-summary")]
    public async Task<IActionResult> YearlySummary([FromQuery] int year)
    {
        if (year < 2000 || year > 2100)
            return BadRequest("Año inválido.");

        return Ok(await _service.GetYearlySummaryAsync(year));
    }

    /// <summary>Category breakdown for a given month.</summary>
    [HttpGet("category-summary")]
    public async Task<IActionResult> CategorySummary(
        [FromQuery] int year, [FromQuery] int month, [FromQuery] string? type)
    {
        if (year < 2000 || year > 2100 || month < 1 || month > 12)
            return BadRequest("Año o mes inválido.");

        return Ok(await _service.GetCategorySummaryAsync(year, month, type));
    }
}
