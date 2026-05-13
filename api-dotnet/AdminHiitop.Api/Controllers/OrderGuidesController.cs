using AdminHiitop.Api.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[Route("api")]
public sealed class OrderGuidesController : BaseApiController
{
    private readonly IOrderGuideService _orderGuideService;

    public OrderGuidesController(IOrderGuideService orderGuideService)
    {
        _orderGuideService = orderGuideService;
    }

    [HttpGet("guides")]
    public async Task<IActionResult> Index()
    {
        return Ok(await _orderGuideService.GetGuidesAsync());
    }

    [HttpGet("orders/{orderId:int}/guide")]
    public async Task<IActionResult> Show(int orderId)
        => await _orderGuideService.GetByOrderIdAsync(orderId) is { } entity ? Ok(entity) : NotFound();

    [HttpPost("orders/{orderId:int}/guide/send")]
    public async Task<IActionResult> Send(int orderId)
    {
        object? response = await _orderGuideService.SendAsync(orderId);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("orders/{orderId:int}/guide/xml")]
    public async Task<IActionResult> Xml(int orderId)
    {
        var file = await _orderGuideService.GetXmlAsync(orderId);
        if (file is null) return NotFound(new { message = "XML no disponible." });
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpGet("orders/{orderId:int}/guide/cdr")]
    public async Task<IActionResult> Cdr(int orderId)
    {
        var file = await _orderGuideService.GetCdrAsync(orderId);
        if (file is null) return NotFound(new { message = "CDR no disponible." });
        return File(file.Content, file.ContentType, file.FileName);
    }
}
