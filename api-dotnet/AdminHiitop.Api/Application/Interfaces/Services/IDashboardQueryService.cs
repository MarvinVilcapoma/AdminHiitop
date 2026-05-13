using AdminHiitop.Api.Application.DTOs.Dashboard;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IDashboardQueryService
{
    Task<DashboardSummaryResponse> GetSummaryAsync(DashboardSummaryFilterRequest request);
}
