using AdminHiitop.Api.Application.DTOs.Invoices;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Shared.Exceptions;
using Microsoft.AspNetCore.Mvc;
using AdminHiitop.Api.Shared.Helpers;

namespace AdminHiitop.Api.Controllers;

[ApiController]
[Route("api/invoices")]
public sealed class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly IInvoiceDeliveryService _deliveryService;
    private readonly IEmailService _emailService;

    public InvoicesController(IInvoiceService invoiceService, IInvoiceDeliveryService deliveryService, IEmailService emailService)
    {
        _invoiceService  = invoiceService;
        _deliveryService = deliveryService;
        _emailService    = emailService;
    }

    /// <summary>Returns the daily email send counter so staff can monitor Brevo usage.</summary>
    [HttpGet("email-status")]
    public async Task<IActionResult> EmailStatus()
    {
        var (sentToday, limit) = await _emailService.GetDailyStatusAsync();
        return Ok(new
        {
            configured  = _emailService.IsConfigured,
            sent_today  = sentToday,
            daily_limit = limit,
            remaining   = Math.Max(0, limit - sentToday),
            reset_at    = PeruClock.Now.Date.AddDays(1).ToString("yyyy-MM-dd"),
        });
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery(Name = "per_page")] int perPage = 15,
        [FromQuery] int page = 1)
        => Ok(await _invoiceService.GetAsync(perPage, page));

    [HttpGet("series")]
    public async Task<IActionResult> Series()
        => Ok(await _invoiceService.GetSeriesAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => await _invoiceService.GetByIdAsync(id) is { } entity ? Ok(entity) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInvoiceRequest request)
        => Ok(await _invoiceService.CreateAsync(request));

    [HttpPost("test-connection")]
    public async Task<IActionResult> TestConnection()
        => Ok(await _invoiceService.TestConnectionAsync());

    [HttpPost("{id:int}/send")]
    public async Task<IActionResult> Send(int id)
        => Ok(await _invoiceService.SendAsync(id));

    [HttpGet("{id:int}/void-check")]
    public async Task<IActionResult> VoidCheck(int id)
        => Ok(await _invoiceService.GetVoidCheckAsync(id));

    [HttpPost("{id:int}/void")]
    public async Task<IActionResult> Void(int id, [FromBody] VoidInvoiceRequest request)
        => Ok(await _invoiceService.VoidAsync(id, request));

    [HttpGet("{id:int}/xml")]
    public async Task<IActionResult> Xml(int id)
    {
        var file = await _invoiceService.GetXmlAsync(id);
        return file is null ? NotFound(new { message = "XML no disponible." }) : File(file.Content, "application/octet-stream", file.FileName);
    }

    [HttpGet("{id:int}/cdr")]
    public async Task<IActionResult> Cdr(int id)
    {
        var file = await _invoiceService.GetCdrAsync(id);
        return file is null ? NotFound(new { message = "CDR no disponible." }) : File(file.Content, "application/octet-stream", file.FileName);
    }

    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> Pdf(int id)
    {
        var file = await _invoiceService.GetPdfAsync(id);
        if (file is null || file.Content.Length == 0)
            return NotFound(new { message = "PDF no disponible. Intenta enviar el comprobante a SUNAT primero." });
        string contentType = file.FileName.EndsWith(".zip") ? "application/octet-stream" : "application/pdf";
        return File(file.Content, contentType, file.FileName);
    }

    /// <summary>
    /// Generates a WhatsApp Click-to-Chat URL with a pre-filled message that includes
    /// the invoice number and Nubefact PDF link. The user must press Send manually.
    /// </summary>
    [HttpGet("{id:int}/whatsapp-link")]
    public async Task<IActionResult> WhatsAppLink(
        int id,
        [FromQuery] string phone,
        [FromQuery(Name = "country_code")] string countryCode = "51")
    {
        try
        {
            var result = await _deliveryService.GetWhatsAppLinkAsync(id, phone, countryCode);
            return Ok(new { success = true, message = "Enlace de WhatsApp generado correctamente.", data = result });
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Re-sends the invoice to the customer's email by calling Nubefact's API with
    /// enviar_automaticamente_al_cliente = true. Nubefact identifies the existing document
    /// by serie + número and delivers it to the provided email address.
    /// </summary>
    [HttpPost("{id:int}/send-email")]
    public async Task<IActionResult> SendEmail(int id, [FromBody] SendInvoiceEmailRequest request)
    {
        try
        {
            var result = await _deliveryService.SendEmailViaNubefactAsync(id, request);
            int statusCode = result.Success ? 200 : 422;
            return StatusCode(statusCode, result);
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { success = false, message = ex.Message });
        }
    }
}
