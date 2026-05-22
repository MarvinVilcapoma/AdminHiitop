using AdminHiitop.Api.Application.DTOs.Orders;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Infrastructure.Auth;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[Route("api/orders")]
public sealed class OrdersController : BaseApiController
{
    private readonly IOrderService _orderService;
    private readonly SessionTokenStore _sessionTokenStore;

    public OrdersController(IOrderService orderService, SessionTokenStore sessionTokenStore)
    {
        _orderService = orderService;
        _sessionTokenStore = sessionTokenStore;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? search,
        [FromQuery(Name = "per_page")]        int? perPage      = null,
        [FromQuery]                           int  page         = 1,
        [FromQuery(Name = "with_summary")]    int? withSummary  = null,
        [FromQuery(Name = "order_status_id")] int? orderStatusId = null,
        [FromQuery(Name = "user_id")]         int? userId        = null,
        [FromQuery]                           string? source      = null)
    {
        return Ok(await _orderService.GetAsync(search, perPage, page, withSummary == 1, orderStatusId, userId, source));
    }

    [HttpGet("monthly-stats")]
    public async Task<IActionResult> GetMonthlyStats([FromQuery] int? year = null)
        => Ok(await _orderService.GetMonthlyStatsAsync(year ?? DateTime.Now.Year));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        Order? entity = await _orderService.GetByIdAsync(id);
        return entity is null ? NotFound() : Ok(entity);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] OrderUpsertRequest request)
    {
        string? token = AuthHeaderHelper.ReadBearerToken(Request);
        int? userId = _sessionTokenStore.GetUserId(token);
        if (userId.HasValue)
        {
            request.UserId = userId.Value;
        }

        return Ok(await _orderService.CreateAsync(request));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] OrderUpsertRequest request) => Ok(await _orderService.UpdateAsync(id, request));

    [HttpPut("{id:int}/tracking")]
    public async Task<IActionResult> UpdateTracking(int id, [FromBody] OrderTrackingUpdateRequest request)
        => Ok(await _orderService.UpdateTrackingAsync(id, request));

    [HttpPut("{id:int}/change-status/{orderStatusId:int}")]
    public async Task<IActionResult> ChangeStatus(int id, int orderStatusId) => Ok(await _orderService.ChangeStatus(id, orderStatusId));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _orderService.DeleteAsync(id);
        return Ok(new { success = true });
    }
}
