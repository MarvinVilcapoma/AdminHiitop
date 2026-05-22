using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IWarehouseTypeService
{
    Task<object> GetAsync(int perPage, int page, string? search);
    Task<WarehouseType?> GetByIdAsync(int id);
    Task<WarehouseType> CreateAsync(WarehouseType request);
    Task<WarehouseType> UpdateAsync(int id, WarehouseType request);
    Task DeleteAsync(int id);
}
