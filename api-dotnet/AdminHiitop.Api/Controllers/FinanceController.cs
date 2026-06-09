using AdminHiitop.Api.Application.DTOs.Finance;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Infrastructure.Auth;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

/// <summary>
/// Enhanced Finance API — profit metrics, order integration, investments.
/// The original /api/financial-* endpoints are kept intact for backward compatibility.
/// </summary>
[Route("api/finance")]
public sealed class FinanceController : BaseApiController
{
    private readonly IFinanceDashboardService          _dashboard;
    private readonly IOrderFinanceIntegrationService   _orderIntegration;
    private readonly IInvestmentService                _investments;
    private readonly SessionTokenStore                 _session;

    public FinanceController(
        IFinanceDashboardService        dashboard,
        IOrderFinanceIntegrationService orderIntegration,
        IInvestmentService              investments,
        SessionTokenStore               session)
    {
        _dashboard        = dashboard;
        _orderIntegration = orderIntegration;
        _investments      = investments;
        _session          = session;
    }

    private int? CurrentUserId => _session.GetUserId(AuthHeaderHelper.ReadBearerToken(Request));

    // ── Dashboard ─────────────────────────────────────────────────────────────

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] int? year, [FromQuery] int? month)
    {
        var now  = DateTime.UtcNow;
        var data = await _dashboard.GetDashboardAsync(year ?? now.Year, month ?? now.Month);
        return Ok(data);
    }

    // ── Order integration ─────────────────────────────────────────────────────

    [HttpPost("orders/{orderId:int}/generate-movement")]
    public async Task<IActionResult> GenerateMovement(int orderId)
    {
        int? movId = await _orderIntegration.GenerateFromOrderAsync(orderId, CurrentUserId);
        if (movId is null)
            return Ok(new { message = "El pedido no genera ingreso (estado no válido o cancelado)." });
        return Ok(new { movement_id = movId, message = "Movimiento financiero generado correctamente." });
    }

    [HttpPost("sync-orders")]
    public async Task<IActionResult> SyncOrders()
        => Ok(await _orderIntegration.SyncAllOrdersAsync(CurrentUserId));

    [HttpPost("movements/{movementId:int}/adjust")]
    public async Task<IActionResult> CreateAdjustment(int movementId, [FromBody] AdjustmentRequest request)
    {
        await _orderIntegration.CreateAdjustmentAsync(movementId, request.Reason, CurrentUserId);
        return Ok(new { message = "Ajuste registrado correctamente." });
    }

    // ── Pending cost ──────────────────────────────────────────────────────────

    [HttpGet("pending-cost-orders")]
    public async Task<IActionResult> GetPendingCostOrders()
        => Ok(await _dashboard.GetPendingCostOrdersAsync());

    // ── Profit reports ────────────────────────────────────────────────────────

    [HttpGet("profit-by-product")]
    public async Task<IActionResult> GetProfitByProduct(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var now    = DateTime.UtcNow;
        var result = await _dashboard.GetProfitByProductAsync(
            from ?? new DateTime(now.Year, now.Month, 1),
            to   ?? now);
        return Ok(result);
    }

    // ── Investments ───────────────────────────────────────────────────────────

    [HttpGet("investment-categories")]
    public async Task<IActionResult> GetInvestmentCategories()
        => Ok(await _investments.GetCategoriesAsync());

    [HttpGet("investments")]
    public async Task<IActionResult> GetInvestments()
        => Ok(await _investments.GetAllAsync());

    [HttpPost("investments")]
    public async Task<IActionResult> CreateInvestment([FromBody] CreateInvestmentRequest request)
    {
        var result = await _investments.CreateAsync(request, CurrentUserId);
        return Created($"api/finance/investments/{result.Id}", result);
    }

    [HttpPut("investments/{id:int}")]
    public async Task<IActionResult> UpdateInvestment(int id, [FromBody] UpdateInvestmentRequest request)
        => Ok(await _investments.UpdateAsync(id, request, CurrentUserId));

    [HttpDelete("investments/{id:int}")]
    public async Task<IActionResult> DeleteInvestment(int id)
    {
        await _investments.DeleteAsync(id);
        return NoContent();
    }
}

public sealed class AdjustmentRequest
{
    public string Reason { get; set; } = string.Empty;
}
