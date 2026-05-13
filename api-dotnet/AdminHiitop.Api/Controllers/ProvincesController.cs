using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[ApiController]
[Route("api/provinces")]
public sealed class ProvincesController : ControllerBase
{
    private readonly IProvinceService _provinceService;

    public ProvincesController(IProvinceService provinceService) => _provinceService = provinceService;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery(Name = "per_page")] int perPage = 15,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
        => Ok(await _provinceService.GetAsync(perPage, page, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        => await _provinceService.GetByIdAsync(id, cancellationToken) is { } entity ? Ok(entity) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Province request, CancellationToken cancellationToken)
        => Ok(await _provinceService.CreateAsync(request, cancellationToken));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] Province request, CancellationToken cancellationToken)
        => Ok(await _provinceService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _provinceService.DeleteAsync(id, cancellationToken);
        return Ok(new { success = true });
    }
}
