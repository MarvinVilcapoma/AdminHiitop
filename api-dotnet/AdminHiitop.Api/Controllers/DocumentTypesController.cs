using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[Route("api/document-types")]
public sealed class DocumentTypesController : BaseApiController
{
    private readonly IDocumentTypeService _documentTypeService;

    public DocumentTypesController(IDocumentTypeService documentTypeService)
    {
        _documentTypeService = documentTypeService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery(Name = "per_page")]   int? perPage    = null,
        [FromQuery]                      int  page       = 1,
        [FromQuery]                      string? search  = null,
        [FromQuery(Name = "active_only")] int? activeOnly = null)
    {
        return Ok(await _documentTypeService.GetAsync(perPage, page, search, activeOnly == 1));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        return Ok(await _documentTypeService.GetByIdAsync(id));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DocumentType request)
    {
        return Ok(await _documentTypeService.CreateAsync(request));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] DocumentType request)
    {
        return Ok(await _documentTypeService.UpdateAsync(id, request));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _documentTypeService.DeleteAsync(id);
        return Ok(new { success = true });
    }
}
