using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Sales.Entities;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[Route("api/invoice-series")]
public sealed class InvoiceSeriesController : BaseApiController
{
    private readonly IInvoiceSeriesService _invoiceSeriesService;

    public InvoiceSeriesController(IInvoiceSeriesService invoiceSeriesService)
    {
        _invoiceSeriesService = invoiceSeriesService;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery(Name = "per_page")] int perPage = 100, [FromQuery] int page = 1)
        => Ok(await _invoiceSeriesService.GetAsync(perPage, page));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] InvoiceSeries request) => Ok(await _invoiceSeriesService.CreateAsync(request));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] InvoiceSeries request) => Ok(await _invoiceSeriesService.UpdateAsync(id, request));
}