using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService) => _customerService = customerService;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? search)
        => Ok(await _customerService.GetAsync(search));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => await _customerService.GetByIdAsync(id) is { } customer
            ? Ok(customer)
            : NotFound(new { message = "Cliente no encontrado." });

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Customer request)
        => Ok(await _customerService.CreateAsync(request));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] Customer request)
        => Ok(await _customerService.UpdateAsync(id, request));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _customerService.DeleteAsync(id);
        return Ok(new { success = true });
    }
}
