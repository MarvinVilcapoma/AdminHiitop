using AdminHiitop.Api.Application.DTOs.Products;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IProductQueryService
{
    Task<IReadOnlyList<ProductListItemResponse>> GetAsync(string? search);
}
