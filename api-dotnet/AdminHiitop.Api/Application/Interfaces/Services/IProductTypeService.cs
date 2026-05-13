using AdminHiitop.Api.Application.DTOs.ProductTypes;
using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IProductTypeService
{
    Task<object> GetAsync(int perPage, int page, string? search, CancellationToken cancellationToken);
    Task<ProductType?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<ProductType> CreateAsync(ProductType request, CancellationToken cancellationToken);
    Task<ProductType> UpdateAsync(int id, ProductType request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
    Task<object> SyncSizesAsync(int productTypeId, SyncSizesRequest request, CancellationToken cancellationToken);
}
