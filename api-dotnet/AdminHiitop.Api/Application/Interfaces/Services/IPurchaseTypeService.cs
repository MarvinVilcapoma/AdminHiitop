using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IPurchaseTypeService
{
    Task<object> GetAsync(int perPage, int page, string? search, CancellationToken cancellationToken);
    Task<PurchaseType?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<PurchaseType> CreateAsync(PurchaseType request, CancellationToken cancellationToken);
    Task<PurchaseType> UpdateAsync(int id, PurchaseType request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
