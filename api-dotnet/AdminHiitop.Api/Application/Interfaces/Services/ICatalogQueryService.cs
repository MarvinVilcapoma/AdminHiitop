using AdminHiitop.Api.Application.DTOs.Catalogs;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface ICatalogQueryService
{
    Task<IReadOnlyList<CatalogItemResponse>> GetDocumentTypesAsync();
    Task<IReadOnlyList<CatalogItemResponse>> GetWarehousesAsync();
    Task<IReadOnlyList<CatalogItemResponse>> GetPaymentMethodsAsync();
}
