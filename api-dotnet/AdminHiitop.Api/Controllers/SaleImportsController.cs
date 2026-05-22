using AdminHiitop.Api.Application.DTOs.SaleImports;
using AdminHiitop.Api.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[ApiController]
[Route("api/sale-imports")]
public sealed class SaleImportsController : ControllerBase
{
    private readonly ISaleImportService _saleImportService;

    public SaleImportsController(ISaleImportService saleImportService) => _saleImportService = saleImportService;

    [HttpGet]
    public async Task<IActionResult> Get()
        => Ok(await _saleImportService.GetAsync());

    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
        => Ok(await _saleImportService.GetSummaryAsync());

    [HttpGet("{batch}")]
    public async Task<IActionResult> Show(string batch)
        => Ok(await _saleImportService.GetByBatchAsync(batch));

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] ImportRowsRequest request)
        => Ok(await _saleImportService.ImportAsync(request));

    [HttpDelete("{batch}/batch")]
    public async Task<IActionResult> DestroyBatch(string batch)
    {
        await _saleImportService.DeleteBatchAsync(batch);
        return Ok(new { success = true });
    }
}
