using AdminHiitop.Api.Application.DTOs.Products;
using AdminHiitop.Api.Domain.Inventory.Entities;
using AdminHiitop.Api.Shared.Models;

namespace AdminHiitop.Api.Application.Interfaces.Repositories;

public interface IProductRepository
{
    Task<PagedResponse<ProductPagedItemResponse>> GetPagedAsync(ProductQueryRequest request);
    Task<IReadOnlyList<ProductListItemResponse>> GetListAsync(string? search);
    Task<Product?> GetByIdAsync(int id);
    Task<ProductDetailResponse?> GetDetailByIdAsync(int id);
    Task<bool> ExistsBySkuAsync(string? sku, int? excludedProductId = null);
    Task AddAsync(Product product);
    Task DeleteAsync(Product product);
    Task SaveChangesAsync();
}
