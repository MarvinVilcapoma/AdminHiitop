using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IPurchaseTypeService
{
    Task<object> GetAsync(int perPage, int page, string? search);
    Task<PurchaseType?> GetByIdAsync(int id);
    Task<PurchaseType> CreateAsync(PurchaseType request);
    Task<PurchaseType> UpdateAsync(int id, PurchaseType request);
    Task DeleteAsync(int id);
}
