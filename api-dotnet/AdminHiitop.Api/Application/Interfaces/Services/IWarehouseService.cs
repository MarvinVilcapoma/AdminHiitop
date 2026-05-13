using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IWarehouseService
{
    Task<object> GetAsync(int? perPage, int page, string? search, CancellationToken cancellationToken);
    Task<Warehouse?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<Warehouse> CreateAsync(Warehouse request, CancellationToken cancellationToken);
    Task<Warehouse> UpdateAsync(int id, Warehouse request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
