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
        [FromQuery] string? search = null)
        => Ok(await _unitMeasureService.GetAsync(perPage, page, search));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => await _unitMeasureService.GetByIdAsync(id) is { } entity ? Ok(entity) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UnitMeasure request)
        => Ok(await _unitMeasureService.CreateAsync(request));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UnitMeasure request)
        => Ok(await _unitMeasureService.UpdateAsync(id, request));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _unitMeasureService.DeleteAsync(id);
        return Ok(new { success = true });
    }
}
