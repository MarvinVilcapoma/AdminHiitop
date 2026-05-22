using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IDailySummaryService
{
    Task<object> GetAsync(int perPage, int page);
    Task<DailySummary?> GetByIdAsync(int id);
    Task<DailySummary> SendAsync(DateTime? date);
    Task<DailySummary> CheckTicketAsync(int id);
}
