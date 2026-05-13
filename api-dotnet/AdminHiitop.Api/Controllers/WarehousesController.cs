using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[ApiController]
[Route("api/warehouses")]
public sealed class WarehousesController : ControllerBase
{
    private readonly IWarehouseService _warehouseService;

    public WarehousesController(IWarehouseService warehouseService) => _warehouseService = warehouseService;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery(Name = "per_page")] int? perPage,
        [FromQuery] int page = 1,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
        => Ok(await _warehouseService.GetAsync(perPage, page, search, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        => await _warehouseService.GetByIdAsync(id, cancellationToken) is { } entity ? Ok(entity) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Warehouse request, CancellationToken cancellationToken)
        => Ok(await _warehouseService.CreateAsync(request, cancellationToken));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] Warehouse request, CancellationToken cancellationToken)
        => Ok(await _warehouseService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _warehouseService.DeleteAsync(id, cancellationToken);
        return Ok(new { success = true });
    }
}
