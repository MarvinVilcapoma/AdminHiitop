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
        [FromQuery(Name = "include_shopify")] int? includeShopify = null)
        => Ok(await _productTypeService.GetAsync(perPage, page, search, includeShopify == 1));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => await _productTypeService.GetByIdAsync(id) is { } entity ? Ok(entity) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProductType request)
        => Ok(await _productTypeService.CreateAsync(request));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ProductType request)
        => Ok(await _productTypeService.UpdateAsync(id, request));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _productTypeService.DeleteAsync(id);
        return Ok(new { success = true });
    }

    [HttpPost("{productTypeId:int}/sizes")]
    public async Task<IActionResult> SyncSizes(int productTypeId, [FromBody] SyncSizesRequest request)
        => Ok(await _productTypeService.SyncSizesAsync(productTypeId, request));
}
