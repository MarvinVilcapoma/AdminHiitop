using AdminHiitop.Api.Application.DTOs.Shopify;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IShopifyOrderService
{
    Task<ShopifyOrderListResponse> GetOrdersAsync(
        int     limit              = 50,
        string? pageInfo           = null,
        string? financialStatus    = null,
        string? fulfillmentStatus  = null,
        string? createdAtMin       = null,
        string? createdAtMax       = null,
        string? search             = null);

    Task<ShopifyOrderResponse?> GetOrderAsync(long orderId);

    Task<object> FulfillOrderAsync(long orderId, string? trackingNumber = null, string? trackingCompany = null);

    Task<object> CancelOrderAsync(long orderId);

    Task<bool> TestConnectionAsync();

    /// <summary>
    /// Fetches up to maxOrders orders by paginating through Shopify (each page max 250).
    /// Filters applied server-side where possible; search is client-side.
    /// </summary>
    /// <summary>
    /// Fetches up to maxOrders orders (pass 0 for unlimited — loads all pages from Shopify).
    /// </summary>
    Task<ShopifyOrderListResponse> GetAllOrdersHistoryAsync(
        int     maxOrders         = 250,
        string? financialStatus   = null,
        string? fulfillmentStatus = null,
        string? createdAtMin      = null,
        string? createdAtMax      = null,
        string? search            = null);

    /// <summary>Returns a paginated list of Shopify customers.</summary>
    Task<ShopifyCustomerListResponse> GetCustomersAsync(
        string? search   = null,
        int     limit    = 50,
        string? pageInfo = null);
}
