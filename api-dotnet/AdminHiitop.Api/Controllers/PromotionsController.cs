using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Sales.Entities;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[ApiController]
[Route("api/promotions")]
public sealed class PromotionsController : ControllerBase
{
    private readonly IPromotionService _promotionService;

    public PromotionsController(IPromotionService promotionService) => _promotionService = promotionService;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery(Name = "per_page")] int perPage = 15,
        [FromQuery] int page = 1,
        [FromQuery] string? search = null,
        [FromQuery(Name = "active_only")]   int? activeOnly   = null,
        [FromQuery(Name = "inactive_only")] int? inactiveOnly = null,
        CancellationToken cancellationToken = default)
        => Ok(await _promotionService.GetAsync(perPage, page, search, activeOnly == 1, inactiveOnly == 1, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        => await _promotionService.GetByIdAsync(id, cancellationToken) is { } entity ? Ok(entity) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Promotion request, CancellationToken cancellationToken)
        => Ok(await _promotionService.CreateAsync(request, cancellationToken));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] Promotion request, CancellationToken cancellationToken)
        => Ok(await _promotionService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _promotionService.DeleteAsync(id, cancellationToken);
        return Ok(new { success = true });
    }
}
