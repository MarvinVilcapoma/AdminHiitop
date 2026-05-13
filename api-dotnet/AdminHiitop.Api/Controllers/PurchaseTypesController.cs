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
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
        => Ok(await _purchaseTypeService.GetAsync(perPage, page, search, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        => await _purchaseTypeService.GetByIdAsync(id, cancellationToken) is { } entity ? Ok(entity) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PurchaseType request, CancellationToken cancellationToken)
        => Ok(await _purchaseTypeService.CreateAsync(request, cancellationToken));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] PurchaseType request, CancellationToken cancellationToken)
        => Ok(await _purchaseTypeService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _purchaseTypeService.DeleteAsync(id, cancellationToken);
        return Ok(new { success = true });
    }
}
