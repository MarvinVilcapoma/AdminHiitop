using AdminHiitop.Api.Application.DTOs.Finance;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Infrastructure.Auth;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[Route("api/fixed-financial-movements")]
public sealed class FixedFinancialMovementsController : BaseApiController
{
    private readonly IFixedFinancialMovementService _service;
    private readonly SessionTokenStore _sessionTokenStore;

    public FixedFinancialMovementsController(IFixedFinancialMovementService service, SessionTokenStore sessionTokenStore)
    {
        _service = service;
        _sessionTokenStore = sessionTokenStore;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? type, [FromQuery] bool? is_active)
        => Ok(await _service.GetAllAsync(type, is_active));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] FixedFinancialMovementRequest request)
    {
        int? userId = _sessionTokenStore.GetUserId(AuthHeaderHelper.ReadBearerToken(Request));
        var result = await _service.CreateAsync(request, userId);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] FixedFinancialMovementRequest request)
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

    /// <summary>Generates movements for all active fixed movements for the given month/year.</summary>
    [HttpPost("generate-month")]
    public async Task<IActionResult> GenerateMonth([FromQuery] int year, [FromQuery] int month)
    {
        int? userId = _sessionTokenStore.GetUserId(AuthHeaderHelper.ReadBearerToken(Request));
        int generated = await _service.GenerateMonthAsync(year, month, userId);
        return Ok(new { generated, message = $"Se generaron {generated} movimiento(s)." });
    }
}
