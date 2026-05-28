using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IWarehouseService
{
    Task<object> GetAsync(int? perPage, int page, string? search, bool includeShopify = false);
    Task<Warehouse?> GetByIdAsync(int id);
    Task<Warehouse> CreateAsync(Warehouse request);
    Task<Warehouse> UpdateAsync(int id, Warehouse request);
    Task DeleteAsync(int id);
}
