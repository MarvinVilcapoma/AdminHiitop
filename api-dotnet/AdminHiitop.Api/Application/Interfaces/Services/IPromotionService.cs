using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IPromotionService
{
    Task<object> GetAsync(int perPage, int page, string? search, bool activeOnly, bool inactiveOnly);
    Task<Promotion?> GetByIdAsync(int id);
    Task<Promotion> CreateAsync(Promotion request);
    Task<Promotion> UpdateAsync(int id, Promotion request);
    Task DeleteAsync(int id);
}
