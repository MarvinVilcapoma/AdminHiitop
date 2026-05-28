using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Application.DTOs.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[Route("api/dashboard")]
public sealed class DashboardController : BaseApiController
{
    private readonly IDashboardQueryService _dashboardQueryService;

    public DashboardController(IDashboardQueryService dashboardQueryService)
    {
        _dashboardQueryService = dashboardQueryService;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] DashboardSummaryFilterRequest request)
    {
        DashboardSummaryResponse response = await _dashboardQueryService.GetSummaryAsync(request);
        return Ok(response);
    }

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts([FromQuery] DashboardSummaryFilterRequest request)
        => Ok(await _dashboardQueryService.GetProductsAsync(request));

    [HttpGet("customers")]
    public async Task<IActionResult> GetCustomers([FromQuery] DashboardSummaryFilterRequest request)
        => Ok(await _dashboardQueryService.GetCustomersAsync(request));

    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders([FromQuery] DashboardSummaryFilterRequest request)
        => Ok(await _dashboardQueryService.GetOrdersAsync(request));

    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices([FromQuery] DashboardSummaryFilterRequest request)
        => Ok(await _dashboardQueryService.GetInvoicesAsync(request));

    [HttpGet("analytics-summary")]
    public async Task<IActionResult> GetAnalyticsSummary([FromQuery] DashboardSummaryFilterRequest request)
        => Ok(await _dashboardQueryService.GetAnalyticsSummaryAsync(request));

    [HttpGet("sales-by-day")]
    public async Task<IActionResult> GetSalesByDay([FromQuery] DashboardSummaryFilterRequest request)
        => Ok(await _dashboardQueryService.GetSalesByDayAsync(request));

    [HttpGet("top-products")]
    public async Task<IActionResult> GetTopProducts([FromQuery] DashboardSummaryFilterRequest request)
        => Ok(await _dashboardQueryService.GetTopProductsAsync(request));

    [HttpGet("by-branch")]
    public async Task<IActionResult> GetByBranch([FromQuery] DashboardSummaryFilterRequest request)
        => Ok(await _dashboardQueryService.GetByBranchAsync(request));

    [HttpGet("by-payment-method")]
    public async Task<IActionResult> GetByPaymentMethod([FromQuery] DashboardSummaryFilterRequest request)
        => Ok(await _dashboardQueryService.GetByPaymentMethodAsync(request));

    [HttpGet("by-seller")]
    public async Task<IActionResult> GetBySeller([FromQuery] DashboardSummaryFilterRequest request)
        => Ok(await _dashboardQueryService.GetBySellerAsync(request));

    [HttpGet("sales-by-month")]
    public async Task<IActionResult> GetSalesByMonth([FromQuery] DashboardSummaryFilterRequest request)
        => Ok(await _dashboardQueryService.GetSalesByMonthAsync(request));
}
