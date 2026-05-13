using AdminHiitop.Api.Application.DTOs.ProductTypes;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[ApiController]
[Route("api/product-types")]
public sealed class ProductTypesController : ControllerBase
{
    private readonly IProductTypeService _productTypeService;

    public ProductTypesController(IProductTypeService productTypeService) => _productTypeService = productTypeService;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery(Name = "per_page")] int perPage = 15,
        [FromQuery] int page = 1,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
        => Ok(await _productTypeService.GetAsync(perPage, page, search, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        => await _productTypeService.GetByIdAsync(id, cancellationToken) is { } entity ? Ok(entity) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProductType request, CancellationToken cancellationToken)
        => Ok(await _productTypeService.CreateAsync(request, cancellationToken));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ProductType request, CancellationToken cancellationToken)
        => Ok(await _productTypeService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _productTypeService.DeleteAsync(id, cancellationToken);
        return Ok(new { success = true });
    }

    [HttpPost("{productTypeId:int}/sizes")]
    public async Task<IActionResult> SyncSizes(int productTypeId, [FromBody] SyncSizesRequest request, CancellationToken cancellationToken)
        => Ok(await _productTypeService.SyncSizesAsync(productTypeId, request, cancellationToken));
}
