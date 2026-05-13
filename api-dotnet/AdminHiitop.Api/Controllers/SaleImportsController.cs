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
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => Ok(await _saleImportService.GetAsync(cancellationToken));

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken)
        => Ok(await _saleImportService.GetSummaryAsync(cancellationToken));

    [HttpGet("{batch}")]
    public async Task<IActionResult> Show(string batch, CancellationToken cancellationToken)
        => Ok(await _saleImportService.GetByBatchAsync(batch, cancellationToken));

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] ImportRowsRequest request, CancellationToken cancellationToken)
        => Ok(await _saleImportService.ImportAsync(request, cancellationToken));

    [HttpDelete("{batch}/batch")]
    public async Task<IActionResult> DestroyBatch(string batch, CancellationToken cancellationToken)
    {
        await _saleImportService.DeleteBatchAsync(batch, cancellationToken);
        return Ok(new { success = true });
    }
}
