using AdminHiitop.Api.Application.DTOs.ProductTypes;
using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IProductTypeService
{
    Task<object> GetAsync(int perPage, int page, string? search, bool includeShopify = false);
    Task<ProductType?> GetByIdAsync(int id);
    Task<ProductType> CreateAsync(ProductType request);
    Task<ProductType> UpdateAsync(int id, ProductType request);
    Task DeleteAsync(int id);
    Task<object> SyncSizesAsync(int productTypeId, SyncSizesRequest request);
}
