using AdminHiitop.Api.Application.DTOs.Products;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IProductService
{
    Task<object> GetAsync(ProductQueryRequest request);
    Task<ProductDetailResponse> GetByIdAsync(int id);
    Task<ProductDetailResponse> CreateAsync(ProductUpsertRequest request);
    Task<ProductDetailResponse> UpdateAsync(int id, ProductUpsertRequest request);
    Task DeleteAsync(int id);
}
