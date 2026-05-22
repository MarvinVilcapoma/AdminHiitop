using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[ApiController]
[Route("api/purchase-types")]
public sealed class PurchaseTypesController : ControllerBase
{
    private readonly IPurchaseTypeService _purchaseTypeService;

    public PurchaseTypesController(IPurchaseTypeService purchaseTypeService) => _purchaseTypeService = purchaseTypeService;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery(Name = "per_page")] int perPage = 15,
        [FromQuery] int page = 1,
        [FromQuery] string? search = null)
        => Ok(await _purchaseTypeService.GetAsync(perPage, page, search));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => await _purchaseTypeService.GetByIdAsync(id) is { } entity ? Ok(entity) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PurchaseType request)
        => Ok(await _purchaseTypeService.CreateAsync(request));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] PurchaseType request)
        => Ok(await _purchaseTypeService.UpdateAsync(id, request));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _purchaseTypeService.DeleteAsync(id);
        return Ok(new { success = true });
    }
}
