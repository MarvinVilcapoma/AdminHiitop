using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[ApiController]
[Route("api/order-statuses")]
public sealed class OrderStatusesController : ControllerBase
{
    private readonly IOrderStatusService _orderStatusService;

    public OrderStatusesController(IOrderStatusService orderStatusService) => _orderStatusService = orderStatusService;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery(Name = "per_page")] int perPage = 15,
        [FromQuery] int page = 1)
        => Ok(await _orderStatusService.GetAsync(perPage, page));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => await _orderStatusService.GetByIdAsync(id) is { } entity ? Ok(entity) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] OrderStatus request)
        => Ok(await _orderStatusService.CreateAsync(request));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] OrderStatus request)
        => Ok(await _orderStatusService.UpdateAsync(id, request));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _orderStatusService.DeleteAsync(id);
        return Ok(new { success = true });
    }
}
