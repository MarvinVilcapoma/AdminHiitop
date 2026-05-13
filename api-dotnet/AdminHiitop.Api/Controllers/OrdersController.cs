using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Sales.Entities;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[Route("api/orders")]
public sealed class OrdersController : BaseApiController
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? search,
        [FromQuery(Name = "per_page")] int? perPage,
        [FromQuery] int page = 1,
        [FromQuery(Name = "with_summary")]    int? withSummary  = null,
        [FromQuery(Name = "order_status_id")] int? orderStatusId = null)
    {
        return Ok(await _orderService.GetAsync(search, perPage, page, withSummary == 1, orderStatusId));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        Order? entity = await _orderService.GetByIdAsync(id);
        return entity is null ? NotFound() : Ok(entity);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Order request) => Ok(await _orderService.CreateAsync(request));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] Order request) => Ok(await _orderService.UpdateAsync(id, request));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _orderService.DeleteAsync(id);
        return Ok(new { success = true });
    }
}
