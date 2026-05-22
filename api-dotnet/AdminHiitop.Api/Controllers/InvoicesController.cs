using AdminHiitop.Api.Application.DTOs.Invoices;
using AdminHiitop.Api.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[ApiController]
[Route("api/invoices")]
public sealed class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;

    public InvoicesController(IInvoiceService invoiceService) => _invoiceService = invoiceService;

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

    [HttpPost("{id:int}/void")]
    public async Task<IActionResult> Void(int id)
        => Ok(await _invoiceService.VoidAsync(id));

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
        return file is null ? NotFound(new { message = "PDF no disponible." }) : File(file.Content, "application/octet-stream", file.FileName);
    }
}
