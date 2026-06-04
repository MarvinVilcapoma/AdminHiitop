using AdminHiitop.Api.Application.DTOs.Returns;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Shared.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[ApiController]
[Route("api/returns")]
public sealed class ReturnsController : ControllerBase
{
    private readonly IReturnService _returnService;

    public ReturnsController(IReturnService returnService)
        => _returnService = returnService;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery(Name = "per_page")] int perPage = 15,
        [FromQuery] int page = 1,
        [FromQuery] string? search = null)
        => Ok(await _returnService.GetAllAsync(perPage, page, search));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _returnService.GetByIdAsync(id);
        return result is null ? NotFound(new { message = "Solicitud de devolución no encontrada." }) : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReturnRequest request)
    {
        try
        {
            var result = await _returnService.CreateReturnAsync(request);
            return Ok(new { success = true, message = "Devolución registrada correctamente.", data = result });
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { success = false, message = ex.Message });
        }
    }

    [HttpPost("{id:int}/issue-credit-note")]
    public async Task<IActionResult> IssueCreditNote(int id)
    {
        try
        {
            var result = await _returnService.IssueCreditNoteAsync(id);
            return Ok(new { success = true, message = "Nota de crédito emitida correctamente.", data = result });
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { success = false, message = ex.Message });
        }
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelReturnRequest? request = null)
    {
        try
        {
            var result = await _returnService.CancelReturnAsync(id, request?.Reason);
            return Ok(result);
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { success = false, message = ex.Message });
        }
    }

    [HttpGet("customer-credits")]
    public async Task<IActionResult> GetCustomerCredits(
        [FromQuery(Name = "per_page")] int perPage = 20,
        [FromQuery] int page = 1,
        [FromQuery(Name = "customer_id")] int? customerId = null)
        => Ok(await _returnService.GetCustomerCreditsAsync(perPage, page, customerId));
}

public sealed class CancelReturnRequest
{
    public string? Reason { get; set; }
}
