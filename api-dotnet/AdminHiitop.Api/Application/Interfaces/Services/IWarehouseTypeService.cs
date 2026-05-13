using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IWarehouseTypeService
{
    Task<object> GetAsync(int perPage, int page, string? search, CancellationToken cancellationToken);
    Task<WarehouseType?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<WarehouseType> CreateAsync(WarehouseType request, CancellationToken cancellationToken);
    Task<WarehouseType> UpdateAsync(int id, WarehouseType request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
