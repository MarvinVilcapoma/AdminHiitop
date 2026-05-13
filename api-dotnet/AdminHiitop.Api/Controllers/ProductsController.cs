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
    public async Task<IActionResult> Get([FromQuery] ProductQueryRequest request)
    {
        object response = await _productService.GetAsync(request);
        return Ok(response);
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
