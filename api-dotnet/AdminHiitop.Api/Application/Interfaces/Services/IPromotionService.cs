using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IPromotionService
{
    Task<object> GetAsync(int perPage, int page, string? search, bool activeOnly, bool inactiveOnly, CancellationToken cancellationToken);
    Task<Promotion?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<Promotion> CreateAsync(Promotion request, CancellationToken cancellationToken);
    Task<Promotion> UpdateAsync(int id, Promotion request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
