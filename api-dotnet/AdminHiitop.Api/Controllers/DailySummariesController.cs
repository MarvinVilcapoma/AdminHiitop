using AdminHiitop.Api.Application.DTOs.DailySummaries;
using AdminHiitop.Api.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[ApiController]
[Route("api/sunat/daily-summaries")]
public sealed class DailySummariesController : ControllerBase
{
    private readonly IDailySummaryService _dailySummaryService;

    public DailySummariesController(IDailySummaryService dailySummaryService) => _dailySummaryService = dailySummaryService;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery(Name = "per_page")] int perPage = 30,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
        => Ok(await _dailySummaryService.GetAsync(perPage, page, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        => await _dailySummaryService.GetByIdAsync(id, cancellationToken) is { } entity ? Ok(entity) : NotFound();

    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SendDailySummaryRequest? request, CancellationToken cancellationToken)
        => Ok(await _dailySummaryService.SendAsync(request?.Date, cancellationToken));

    [HttpPost("{id:int}/check-ticket")]
    public async Task<IActionResult> CheckTicket(int id, CancellationToken cancellationToken)
        => Ok(await _dailySummaryService.CheckTicketAsync(id, cancellationToken));
}
