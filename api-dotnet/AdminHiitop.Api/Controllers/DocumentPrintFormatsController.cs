using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[Route("api/document-print-formats")]
public sealed class DocumentPrintFormatsController : BaseApiController
{
    private readonly IDocumentPrintFormatService _documentPrintFormatService;

    public DocumentPrintFormatsController(IDocumentPrintFormatService documentPrintFormatService)
    {
        _documentPrintFormatService = documentPrintFormatService;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery(Name = "per_page")] int perPage = 15, [FromQuery] int page = 1, [FromQuery] string? search = null)
    {
        return Ok(await _documentPrintFormatService.GetAsync(perPage, page, search));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id) => await _documentPrintFormatService.GetByIdAsync(id) is { } entity ? Ok(entity) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DocumentPrintFormat request) => Ok(await _documentPrintFormatService.CreateAsync(request));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] DocumentPrintFormat request) => Ok(await _documentPrintFormatService.UpdateAsync(id, request));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _documentPrintFormatService.DeleteAsync(id);
        return Ok(new { success = true });
    }
}
