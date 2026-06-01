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
        [FromQuery(Name = "include_shopify")] int? includeShopify = null)
        => Ok(await _warehouseService.GetAsync(perPage, page, search, includeShopify == 1));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => await _warehouseService.GetByIdAsync(id) is { } entity ? Ok(entity) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Warehouse request)
        => Ok(await _warehouseService.CreateAsync(request));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] Warehouse request)
        => Ok(await _warehouseService.UpdateAsync(id, request));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _warehouseService.DeleteAsync(id);
        return Ok(new { success = true });
    }

    [HttpPost("shopify-sync")]
    public async Task<IActionResult> SyncShopify()
    {
        await _warehouseService.SyncShopifyLocationsAsync();
        return Ok(new { success = true, message = "Ubicaciones de Shopify sincronizadas correctamente." });
    }
}
