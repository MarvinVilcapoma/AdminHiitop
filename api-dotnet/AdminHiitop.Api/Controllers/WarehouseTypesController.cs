using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[ApiController]
[Route("api/warehouse-types")]
public sealed class WarehouseTypesController : ControllerBase
{
    private readonly IWarehouseTypeService _warehouseTypeService;

    public WarehouseTypesController(IWarehouseTypeService warehouseTypeService) => _warehouseTypeService = warehouseTypeService;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery(Name = "per_page")] int perPage = 15,
        [FromQuery] int page = 1,
        [FromQuery] string? search = null)
        => Ok(await _warehouseTypeService.GetAsync(perPage, page, search));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => await _warehouseTypeService.GetByIdAsync(id) is { } entity ? Ok(entity) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] WarehouseType request)
        => Ok(await _warehouseTypeService.CreateAsync(request));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] WarehouseType request)
        => Ok(await _warehouseTypeService.UpdateAsync(id, request));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _warehouseTypeService.DeleteAsync(id);
        return Ok(new { success = true });
    }
}
