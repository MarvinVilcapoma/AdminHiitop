using AdminHiitop.Api.Application.DTOs.Products;
using AdminHiitop.Api.Application.Helpers;
using AdminHiitop.Api.Application.Interfaces.Repositories;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Inventory.Entities;
using AdminHiitop.Api.Shared.Exceptions;

namespace AdminHiitop.Api.Application.Services.Products;

public sealed class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;

    public ProductService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<object> GetAsync(ProductQueryRequest request)
    {
        if (request.PerPage.HasValue)
        {
            return await _productRepository.GetPagedAsync(request);
        }

        return await _productRepository.GetListAsync(request.Search);
    }

    public async Task<ProductDetailResponse> GetByIdAsync(int id)
    {
        ProductDetailResponse? response = await _productRepository.GetDetailByIdAsync(id);

        if (response is null)
        {
            throw new AppException("Producto no encontrado.", 404);
        }

        return response;
    }

    public async Task<ProductDetailResponse> CreateAsync(ProductUpsertRequest request)
    {
        InventoryValidationHelper.ValidateProduct(request);
        await EnsureSkuAvailableAsync(request.Sku, null);

        Product product = new()
        {
            Name = request.Name.Trim(),
            Sku = string.IsNullOrWhiteSpace(request.Sku) ? null : request.Sku.Trim(),
            ProductTypeId = request.ProductTypeId,
            CollectionId = request.CollectionId,
            UnitMeasureId = request.UnitMeasureId,
            Description = request.Description,
            BasePrice = request.BasePrice,
            UnitCost = request.UnitCost,
            IsActive = request.IsActive
        };

        await _productRepository.AddAsync(product);
        await _productRepository.SaveChangesAsync();

        return await GetByIdAsync(product.Id);
    }

    public async Task<ProductDetailResponse> UpdateAsync(int id, ProductUpsertRequest request)
    {
        InventoryValidationHelper.ValidateProduct(request);

        Product product = await FindProductAsync(id);
        await EnsureSkuAvailableAsync(request.Sku, id);

        product.Name = request.Name.Trim();
        product.Sku = string.IsNullOrWhiteSpace(request.Sku) ? null : request.Sku.Trim();
        product.ProductTypeId = request.ProductTypeId;
        product.CollectionId = request.CollectionId;
        product.UnitMeasureId = request.UnitMeasureId;
        product.Description = request.Description;
        product.BasePrice = request.BasePrice;
        product.UnitCost = request.UnitCost;
        product.IsActive = request.IsActive;

        await _productRepository.SaveChangesAsync();
        return await GetByIdAsync(product.Id);
    }

    public async Task DeleteAsync(int id)
    {
        Product product = await FindProductAsync(id);
        await _productRepository.DeleteAsync(product);
        await _productRepository.SaveChangesAsync();
    }

    private async Task<Product> FindProductAsync(int id)
    {
        Product? product = await _productRepository.GetByIdAsync(id);

        if (product is null)
        {
            throw new AppException("Producto no encontrado.", 404);
        }

        return product;
    }

    private async Task EnsureSkuAvailableAsync(string? sku, int? excludedProductId)
    {
        bool exists = await _productRepository.ExistsBySkuAsync(sku, excludedProductId);

        if (exists)
        {
            throw new AppException("Ya existe un producto con ese SKU.");
        }
    }
}
