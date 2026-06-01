using AdminHiitop.Api.Application.DTOs.Finance;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Infrastructure.Auth;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[Authorize]
[Route("api/financial-movements")]
public sealed class FinancialMovementsController : BaseApiController
{
    private readonly IFinancialMovementService _service;
    private readonly SessionTokenStore _sessionTokenStore;

    public FinancialMovementsController(IFinancialMovementService service, SessionTokenStore sessionTokenStore)
    {
        _service = service;
        _sessionTokenStore = sessionTokenStore;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string? type,
        [FromQuery] int? category_id,
        [FromQuery] string? payment_method,
        [FromQuery] DateTime? date_from,
        [FromQuery] DateTime? date_to,
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] int page = 1,
        [FromQuery] int per_page = 20)
    {
        var result = await _service.GetPagedAsync(
            type, category_id, payment_method,
            date_from, date_to, year, month,
            Math.Max(1, page), Math.Clamp(per_page, 1, 100));

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] FinancialMovementRequest request)
    {
        int? userId = _sessionTokenStore.GetUserId(AuthHeaderHelper.ReadBearerToken(Request));
        var result = await _service.CreateAsync(request, userId);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] FinancialMovementRequest request)
    {
        int? userId = _sessionTokenStore.GetUserId(AuthHeaderHelper.ReadBearerToken(Request));
        return Ok(await _service.UpdateAsync(id, request, userId));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}
