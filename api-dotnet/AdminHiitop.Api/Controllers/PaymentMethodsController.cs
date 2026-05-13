using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[ApiController]
[Route("api/payment-methods")]
public sealed class PaymentMethodsController : ControllerBase
{
    private readonly IPaymentMethodService _paymentMethodService;

    public PaymentMethodsController(IPaymentMethodService paymentMethodService) => _paymentMethodService = paymentMethodService;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery(Name = "per_page")] int? perPage,
        [FromQuery] int page = 1,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
        => Ok(await _paymentMethodService.GetAsync(perPage, page, search, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        => await _paymentMethodService.GetByIdAsync(id, cancellationToken) is { } entity ? Ok(entity) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PaymentMethod request, CancellationToken cancellationToken)
        => Ok(await _paymentMethodService.CreateAsync(request, cancellationToken));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] PaymentMethod request, CancellationToken cancellationToken)
        => Ok(await _paymentMethodService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _paymentMethodService.DeleteAsync(id, cancellationToken);
        return Ok(new { success = true });
    }
}
