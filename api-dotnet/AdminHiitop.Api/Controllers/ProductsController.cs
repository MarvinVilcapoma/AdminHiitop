using AdminHiitop.Api.Application.DTOs.Common;
using AdminHiitop.Api.Application.DTOs.Products;
using AdminHiitop.Api.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[Route("api/products")]
public sealed class ProductsController : BaseApiController
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery(Name = "per_page")]    int? perPage      = null,
        [FromQuery]                       int  page          = 1,
        [FromQuery]                       string? search     = null,
        [FromQuery(Name = "active_only")]   int? activeOnly   = null,
        [FromQuery(Name = "collection_id")] int? collectionId = null,
        [FromQuery(Name = "warehouse_id")]  int? warehouseId  = null)
    {
        var request = new ProductQueryRequest
        {
            PerPage      = perPage,
            Page         = page,
            Search       = search,
            ActiveOnly   = activeOnly == 1,
            CollectionId = collectionId,
            WarehouseId  = warehouseId,
        };
        return Ok(await _productService.GetAsync(request));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        ProductDetailResponse response = await _productService.GetByIdAsync(id);
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProductUpsertRequest request)
    {
        ProductDetailResponse response = await _productService.CreateAsync(request);
        return Ok(response);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ProductUpsertRequest request)
    {
        ProductDetailResponse response = await _productService.UpdateAsync(id, request);
        return Ok(response);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _productService.DeleteAsync(id);
        return Ok(new SuccessResponse());
    }
}
