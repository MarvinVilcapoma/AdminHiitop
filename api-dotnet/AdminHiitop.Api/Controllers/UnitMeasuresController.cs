using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[ApiController]
[Route("api/unit-measures")]
public sealed class UnitMeasuresController : ControllerBase
{
    private readonly IUnitMeasureService _unitMeasureService;

    public UnitMeasuresController(IUnitMeasureService unitMeasureService) => _unitMeasureService = unitMeasureService;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery(Name = "per_page")] int perPage = 15,
        [FromQuery] int page = 1,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
        => Ok(await _unitMeasureService.GetAsync(perPage, page, search, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        => await _unitMeasureService.GetByIdAsync(id, cancellationToken) is { } entity ? Ok(entity) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UnitMeasure request, CancellationToken cancellationToken)
        => Ok(await _unitMeasureService.CreateAsync(request, cancellationToken));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UnitMeasure request, CancellationToken cancellationToken)
        => Ok(await _unitMeasureService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _unitMeasureService.DeleteAsync(id, cancellationToken);
        return Ok(new { success = true });
    }
}
