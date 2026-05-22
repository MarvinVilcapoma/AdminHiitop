using AdminHiitop.Api.Application.DTOs.Common;
using AdminHiitop.Api.Application.DTOs.Stocks;
using AdminHiitop.Api.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[Route("api/stocks")]
public sealed class StocksController : BaseApiController
{
    private readonly IStockService _stockService;

    public StocksController(IStockService stockService)
    {
        _stockService = stockService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery(Name = "per_page")]       int  perPage       = 15,
        [FromQuery]                          int  page          = 1,
        [FromQuery]                          string? search     = null,
        [FromQuery(Name = "warehouse_id")]   int? warehouseId   = null,
        [FromQuery(Name = "color_id")]       int? colorId       = null,
        [FromQuery(Name = "product_type_id")] int? productTypeId = null,
        [FromQuery(Name = "collection_id")]  int? collectionId  = null,
        [FromQuery(Name = "low_stock")]      int? lowStock      = null)
    {
        var request = new StockQueryRequest
        {
            PerPage       = perPage,
            Page          = page,
            Search        = search,
            WarehouseId   = warehouseId,
            ColorId       = colorId,
            ProductTypeId = productTypeId,
            CollectionId  = collectionId,
            LowStock      = lowStock == 1,
        };
        object response = await _stockService.GetAsync(request);
        return Ok(response);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        IReadOnlyList<StockSummaryResponse> response = await _stockService.GetSummaryAsync();
        return Ok(response);
    }

    [HttpGet("available")]
    public async Task<IActionResult> Available(
        [FromQuery(Name = "product_id")]   int? productId   = null,
        [FromQuery(Name = "warehouse_id")] int? warehouseId = null)
    {
        object response = await _stockService.GetAvailableGroupedAsync(productId, warehouseId);
        return Ok(response);
    }

    [HttpGet("lookup")]
    public async Task<IActionResult> Lookup(
        [FromQuery] string? search = null,
        [FromQuery(Name = "warehouse_id")]   int? warehouseId   = null,
        [FromQuery(Name = "color_id")]       int? colorId       = null,
        [FromQuery(Name = "available_only")] int? availableOnly = null,
        [FromQuery] int limit = 30)
    {
        IReadOnlyList<StockLookupResponse> data = await _stockService.GetLookupAsync(
            search, warehouseId, colorId, availableOnly == 1, Math.Clamp(limit, 1, 200));
        return Ok(new { data });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        StockResponse response = await _stockService.GetByIdAsync(id);
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] StockUpsertRequest request)
    {
        StockResponse response = await _stockService.CreateAsync(request);
        return Ok(response);
    }

    [HttpPost("bulk")]
    public async Task<IActionResult> Bulk([FromBody] List<StockUpsertRequest> request)
    {
        SuccessResponse response = await _stockService.BulkCreateAsync(request);
        return Ok(response);
    }

    [HttpPost("bulk-transfer")]
    public async Task<IActionResult> BulkTransfer([FromBody] StockBulkTransferRequest request)
    {
        SuccessResponse response = await _stockService.BulkTransferAsync(request);
        return Ok(response);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] StockUpsertRequest request)
    {
        StockResponse response = await _stockService.UpdateAsync(id, request);
        return Ok(response);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _stockService.DeleteAsync(id);
        return Ok(new SuccessResponse());
    }

    [HttpPost("{id:int}/adjust")]
    public async Task<IActionResult> Adjust(int id, [FromBody] StockAdjustRequest request)
    {
        StockResponse response = await _stockService.AdjustAsync(id, request);
        return Ok(response);
    }

    [HttpPost("{id:int}/transfer")]
    public async Task<IActionResult> Transfer(int id, [FromBody] StockTransferRequest request)
    {
        SuccessResponse response = await _stockService.TransferAsync(id, request);
        return Ok(response);
    }
}
