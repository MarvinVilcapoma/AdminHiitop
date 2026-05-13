using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Sales.Entities;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[ApiController]
[Route("api/sales")]
public sealed class SalesController : ControllerBase
{
    private readonly ISaleService _saleService;

    public SalesController(ISaleService saleService) => _saleService = saleService;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery(Name = "per_page")] int perPage = 15,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
        => Ok(await _saleService.GetAsync(perPage, page, cancellationToken));

    [HttpGet("branches")]
    public IActionResult Branches() => Ok(_saleService.GetBranches());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        => await _saleService.GetByIdAsync(id, cancellationToken) is { } entity ? Ok(entity) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Sale request, CancellationToken cancellationToken)
        => Ok(await _saleService.CreateAsync(request, cancellationToken));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] Sale request, CancellationToken cancellationToken)
        => Ok(await _saleService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _saleService.DeleteAsync(id, cancellationToken);
        return Ok(new { success = true });
    }
}
