using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IDailySummaryService
{
    Task<object> GetAsync(int perPage, int page, CancellationToken cancellationToken);
    Task<DailySummary?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<DailySummary> SendAsync(DateTime? date, CancellationToken cancellationToken);
    Task<DailySummary> CheckTicketAsync(int id, CancellationToken cancellationToken);
}
