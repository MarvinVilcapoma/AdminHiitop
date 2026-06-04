using AdminHiitop.Api.Application.DTOs.OrderGuides;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Shared.Exceptions;
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

    /// <summary>
    /// Step 2 of the NubeFact two-step process: consult the guide status.
    /// Call this after /send to poll for SUNAT acceptance and get the PDF link.
    /// </summary>
    [HttpPost("orders/{orderId:int}/guide/consult")]
    public async Task<IActionResult> Consult(int orderId)
    {
        object? response = await _orderGuideService.ConsultAsync(orderId);
        return response is null ? NotFound(new { message = "La guía aún no ha sido enviada." }) : Ok(response);
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

    [HttpGet("orders/{orderId:int}/guide/pdf")]
    public async Task<IActionResult> Pdf(int orderId)
    {
        try
        {
            var file = await _orderGuideService.GetPdfAsync(orderId);
            if (file is null) return NotFound(new { message = "PDF de guía no disponible. Consulta el estado en SUNAT primero." });
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (Exception)
        {
            return StatusCode(502, new { message = "No se pudo descargar el PDF desde Nubefact." });
        }
    }

    [HttpPost("orders/{orderId:int}/guide/send-email")]
    public async Task<IActionResult> SendEmail(int orderId, [FromBody] GuideSendEmailRequest request)
    {
        try
        {
            var result = await _orderGuideService.SendEmailAsync(orderId, request.Email);
            return Ok(result);
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(422, new
            {
                success          = false,
                requires_fallback = true,
                message          = "No se pudo enviar el correo. Verifica la configuración SMTP.",
                error            = ex.Message,
            });
        }
    }

    [HttpGet("orders/{orderId:int}/guide/whatsapp-link")]
    public async Task<IActionResult> WhatsAppLink(
        int orderId,
        [FromQuery] string phone,
        [FromQuery(Name = "country_code")] string countryCode = "51")
    {
        try
        {
            var result = await _orderGuideService.GetWhatsAppLinkAsync(orderId, phone, countryCode);
            return Ok(new { success = true, data = result });
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { success = false, message = ex.Message });
        }
    }
}
