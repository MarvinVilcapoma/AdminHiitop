using AdminHiitop.Api.Application.DTOs.Finance;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IFinancialDashboardService
{
    Task<FinancialDashboardResponse> GetMonthlySummaryAsync(int year, int month);
    Task<List<MonthlySummaryItem>> GetYearlySummaryAsync(int year);
    Task<List<CategorySummaryItem>> GetCategorySummaryAsync(int year, int month, string? type = null);
}
