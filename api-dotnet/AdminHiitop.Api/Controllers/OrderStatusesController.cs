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
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
        => Ok(await _orderStatusService.GetAsync(perPage, page, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        => await _orderStatusService.GetByIdAsync(id, cancellationToken) is { } entity ? Ok(entity) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] OrderStatus request, CancellationToken cancellationToken)
        => Ok(await _orderStatusService.CreateAsync(request, cancellationToken));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] OrderStatus request, CancellationToken cancellationToken)
        => Ok(await _orderStatusService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _orderStatusService.DeleteAsync(id, cancellationToken);
        return Ok(new { success = true });
    }
}
