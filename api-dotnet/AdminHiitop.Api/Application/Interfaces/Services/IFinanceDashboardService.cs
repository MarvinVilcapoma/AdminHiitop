using AdminHiitop.Api.Application.DTOs.Finance;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IFinanceDashboardService
{
    Task<FinanceDashboardDto> GetDashboardAsync(int year, int month);
    Task<List<PendingCostOrderDto>> GetPendingCostOrdersAsync();
    Task<List<ProfitByProductDto>> GetProfitByProductAsync(DateTime from, DateTime to);
}
