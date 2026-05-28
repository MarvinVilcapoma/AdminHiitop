using AdminHiitop.Api.Application.DTOs.Shopify;
using AdminHiitop.Api.Application.DTOs.Stocks;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IShopifyProductService
{
    /// <summary>
    /// Returns Shopify variants mapped to StockLookupResponse — same shape as /api/stocks/lookup.
    /// Includes real-time inventory quantities from the configured location.
    /// </summary>
    Task<IReadOnlyList<StockLookupResponse>> GetLookupAsync(string? search, int limit, long? locationId = null);

    /// <summary>Returns paginated Shopify product list with summary info.</summary>
    Task<ShopifyProductListResponse> GetProductsAsync(
        string? search, int page, int perPage, string status = "active", long? locationId = null);

    /// <summary>Returns full product detail with all variants and inventory quantities.</summary>
    Task<ShopifyProductDetailResponse?> GetProductAsync(long productId);

    /// <summary>Updates product metadata (title, description, tags, status).</summary>
    Task<ShopifyProductDetailResponse> UpdateProductAsync(long productId, ShopifyProductUpdateRequest request);

    /// <summary>Updates a single variant (price, SKU, option values).</summary>
    Task<ShopifyVariantResponse> UpdateVariantAsync(long variantId, ShopifyVariantUpdateRequest request);

    /// <summary>Returns all active Shopify locations.</summary>
    Task<List<ShopifyLocationResponse>> GetLocationsAsync();

    /// <summary>Returns order-based sales metrics for the given date range.</summary>
    Task<ShopifyMetricsResponse> GetMetricsAsync(DateTime? startDate, DateTime? endDate);

    /// <summary>
    /// Deducts (or adds) inventory from a Shopify location.
    /// delta &lt; 0 = deduct (used when an order consumes Shopify stock).
    /// </summary>
    Task<bool> AdjustInventoryAsync(long inventoryItemId, long locationId, int delta);

    /// <summary>Sets inventory to an absolute quantity for a specific location.</summary>
    Task<bool> SetInventoryAsync(long inventoryItemId, long locationId, int available);

    /// <summary>Creates a new product in Shopify with optional initial inventory.</summary>
    Task<ShopifyProductDetailResponse> CreateProductAsync(
        ShopifyProductCreateRequest request, long? locationId = null);

    /// <summary>Updates inventory for multiple variants at once (bulk).</summary>
    Task<BulkInventoryUpdateResponse> BulkSetInventoryAsync(BulkInventoryUpdateRequest request);

    /// <summary>Returns all custom + smart Shopify collections.</summary>
    Task<List<ShopifyCollectionResponse>> GetAllCollectionsAsync();

    /// <summary>Returns the collect memberships for a product (collectId + collectionId pairs).</summary>
    Task<List<ShopifyCollectResponse>> GetProductCollectsAsync(long productId);

    /// <summary>Adds product to the given collection IDs and removes it from the given collect IDs.</summary>
    Task UpdateProductCollectionsAsync(long productId, List<long> addCollectionIds, List<long> removeCollectIds);

    /// <summary>Returns inventory levels for all variants of a product across all active locations.</summary>
    Task<List<ShopifyInventoryLevelResponse>> GetProductInventoryAsync(long productId);

    Task<object> GetInventoryLevelAsync(long inventoryItemId, long locationId);

    /// <summary>Transfers inventory between two Shopify locations and records the transfer in the database.</summary>
    Task<ShopifyInventoryTransferResponse> TransferInventoryAsync(
        ShopifyInventoryTransferRequest request, string? performedBy = null);

    /// <summary>Returns the transfer history, most recent first.</summary>
    Task<List<ShopifyInventoryTransferResponse>> GetTransferHistoryAsync(int limit = 100);

    /// <summary>Uploads an image (base64) to a Shopify product and returns the created image.</summary>
    Task<ShopifyImageResponse> UploadProductImageAsync(long productId, string base64Data, string filename);
}
