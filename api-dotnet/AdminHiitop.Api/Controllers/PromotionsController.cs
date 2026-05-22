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
        [FromQuery(Name = "inactive_only")] int? inactiveOnly = null)
        => Ok(await _promotionService.GetAsync(perPage, page, search, activeOnly == 1, inactiveOnly == 1));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => await _promotionService.GetByIdAsync(id) is { } entity ? Ok(entity) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Promotion request)
        => Ok(await _promotionService.CreateAsync(request));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] Promotion request)
        => Ok(await _promotionService.UpdateAsync(id, request));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _promotionService.DeleteAsync(id);
        return Ok(new { success = true });
    }
}
