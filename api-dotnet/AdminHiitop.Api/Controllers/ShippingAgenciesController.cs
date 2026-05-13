using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[ApiController]
[Route("api/shipping-agencies")]
public sealed class ShippingAgenciesController : ControllerBase
{
    private readonly IShippingAgencyService _shippingAgencyService;

    public ShippingAgenciesController(IShippingAgencyService shippingAgencyService) => _shippingAgencyService = shippingAgencyService;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery(Name = "per_page")] int perPage = 15,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
        => Ok(await _shippingAgencyService.GetAsync(perPage, page, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        => await _shippingAgencyService.GetByIdAsync(id, cancellationToken) is { } entity ? Ok(entity) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ShippingAgency request, CancellationToken cancellationToken)
        => Ok(await _shippingAgencyService.CreateAsync(request, cancellationToken));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ShippingAgency request, CancellationToken cancellationToken)
        => Ok(await _shippingAgencyService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _shippingAgencyService.DeleteAsync(id, cancellationToken);
        return Ok(new { success = true });
    }
}
