using AdminHiitop.Api.Application.DTOs.Dashboard;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IDashboardQueryService
{
    Task<DashboardSummaryResponse> GetSummaryAsync(DashboardSummaryFilterRequest request);
    Task<DashboardProductsResponse> GetProductsAsync(DashboardSummaryFilterRequest request);
    Task<DashboardCustomersResponse> GetCustomersAsync(DashboardSummaryFilterRequest request);
    Task<DashboardOrdersResponse> GetOrdersAsync(DashboardSummaryFilterRequest request);
    Task<DashboardInvoicesResponse> GetInvoicesAsync(DashboardSummaryFilterRequest request);
    Task<DashboardAnalyticsSummaryResponse> GetAnalyticsSummaryAsync(DashboardSummaryFilterRequest request);
    Task<IReadOnlyList<DashboardSalesByDayResponse>> GetSalesByDayAsync(DashboardSummaryFilterRequest request);
    Task<IReadOnlyList<DashboardTopProductResponse>> GetTopProductsAsync(DashboardSummaryFilterRequest request);
    Task<IReadOnlyList<DashboardBranchResponse>> GetByBranchAsync(DashboardSummaryFilterRequest request);
    Task<IReadOnlyList<DashboardPaymentMethodBreakdownResponse>> GetByPaymentMethodAsync(DashboardSummaryFilterRequest request);
    Task<IReadOnlyList<DashboardSellerResponse>> GetBySellerAsync(DashboardSummaryFilterRequest request);
}
