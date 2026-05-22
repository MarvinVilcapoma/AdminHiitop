using AdminHiitop.Api.Application.DTOs.Shopify;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Infrastructure.Shopify;

namespace AdminHiitop.Api.Application.Services.Shopify;

public sealed class ShopifyOrderService : IShopifyOrderService
{
    private readonly ShopifyAdminClient _client;

    public ShopifyOrderService(ShopifyAdminClient client)
    {
        _client = client;
    }

    public async Task<ShopifyOrderListResponse> GetOrdersAsync(
        int     limit             = 50,
        string? pageInfo          = null,
        string? financialStatus   = null,
        string? fulfillmentStatus = null,
        string? createdAtMin      = null,
        string? createdAtMax      = null,
        string? search            = null)
    {
        // Use GraphQL for text search: server-side index, same engine as Shopify admin
        if (!string.IsNullOrWhiteSpace(search) && !search.Contains('@'))
        {
            string? gqlAfter = pageInfo?.StartsWith("gql:", StringComparison.Ordinal) == true
                ? pageInfo[4..] : null;
            string gqlQuery = BuildOrderGqlQuery(search, financialStatus, fulfillmentStatus, createdAtMin, createdAtMax);

            ShopifyOrderListResult gqlResult = await _client.SearchOrdersGraphQlAsync(
                gqlQuery, Math.Clamp(limit, 1, 50), gqlAfter);

            return new ShopifyOrderListResponse
            {
                Orders       = gqlResult.Orders.Select(MapOrder).ToList(),
                Count        = gqlResult.Orders.Count,
                NextPageInfo = gqlResult.NextPageInfo is not null ? $"gql:{gqlResult.NextPageInfo}" : null,
                PrevPageInfo = null,
            };
        }

        // Email search or browse without search: use REST cursor pagination (unchanged)
        ShopifyOrderListResult result = await _client.GetOrdersAsync(
            limit, pageInfo, financialStatus, fulfillmentStatus, createdAtMin, createdAtMax, search);

        return new ShopifyOrderListResponse
        {
            Orders       = result.Orders.Select(MapOrder).ToList(),
            Count        = result.Orders.Count,
            NextPageInfo = result.NextPageInfo,
            PrevPageInfo = result.PrevPageInfo,
        };
    }

    public async Task<ShopifyOrderResponse?> GetOrderAsync(long orderId)
    {
        ShopifyApiOrder? order = await _client.GetOrderAsync(orderId);
        return order is null ? null : MapOrder(order);
    }

    public async Task<object> FulfillOrderAsync(long orderId, string? trackingNumber = null, string? trackingCompany = null)
    {
        bool success = await _client.FulfillOrderAsync(orderId, trackingNumber, trackingCompany);
        return new
        {
            success,
            message = success ? "Orden marcada como enviada en Shopify." : "No se pudo completar el fulfillment. Verifica que la orden tenga ítems pendientes."
        };
    }

    public async Task<object> CancelOrderAsync(long orderId)
    {
        bool success = await _client.CancelOrderAsync(orderId);
        return new
        {
            success,
            message = success ? "Orden cancelada en Shopify." : "No se pudo cancelar la orden en Shopify."
        };
    }

    public async Task<ShopifyOrderListResponse> GetAllOrdersHistoryAsync(
        int     maxOrders         = 250,
        string? financialStatus   = null,
        string? fulfillmentStatus = null,
        string? createdAtMin      = null,
        string? createdAtMax      = null,
        string? search            = null)
    {
        List<ShopifyApiOrder> all;

        if (!string.IsNullOrWhiteSpace(search))
        {
            // GraphQL: only matching orders are fetched — no full-history download needed
            int hardCap = maxOrders <= 0 ? 10_000 : maxOrders;
            string gqlQuery = BuildOrderGqlQuery(search, financialStatus, fulfillmentStatus, createdAtMin, createdAtMax);
            all = await _client.GetAllMatchingOrdersGraphQlAsync(gqlQuery, hardCap);
        }
        else
        {
            // No search: fetch all orders for browsing (REST pagination, unchanged)
            all = await _client.GetAllOrdersAsync(
                maxOrders, financialStatus, fulfillmentStatus, createdAtMin, createdAtMax);
        }

        return new ShopifyOrderListResponse
        {
            Orders       = all.Select(MapOrder).ToList(),
            Count        = all.Count,
            NextPageInfo = null,
            PrevPageInfo = null,
        };
    }

    private static string BuildOrderGqlQuery(
        string? search, string? financialStatus, string? fulfillmentStatus,
        string? createdAtMin, string? createdAtMax)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))           parts.Add(search.Trim());
        if (!string.IsNullOrWhiteSpace(createdAtMin))     parts.Add($"created_at:>='{createdAtMin}'");
        if (!string.IsNullOrWhiteSpace(createdAtMax))     parts.Add($"created_at:<='{createdAtMax}'");
        if (!string.IsNullOrWhiteSpace(financialStatus))  parts.Add($"financial_status:{financialStatus}");
        if (!string.IsNullOrWhiteSpace(fulfillmentStatus)) parts.Add($"fulfillment_status:{fulfillmentStatus}");
        return string.Join(" ", parts);
    }

    public async Task<ShopifyCustomerListResponse> GetCustomersAsync(
        string? search   = null,
        int     limit    = 50,
        string? pageInfo = null)
    {
        var (customers, next) = await _client.GetCustomersAsync(search, limit, pageInfo);
        return new ShopifyCustomerListResponse
        {
            Customers    = customers.Select(MapCustomer).ToList(),
            Count        = customers.Count,
            NextPageInfo = next,
        };
    }

    private static ShopifyCustomerResponse MapCustomer(ShopifyApiCustomer c)
    {
        string? name = $"{c.FirstName} {c.LastName}".Trim();
        decimal.TryParse(c.TotalSpent,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out decimal spent);
        return new ShopifyCustomerResponse
        {
            Id            = c.Id,
            Email         = c.Email,
            Name          = string.IsNullOrWhiteSpace(name) ? null : name,
            Phone         = c.Phone,
            OrdersCount   = c.OrdersCount,
            TotalSpent    = spent,
            Tags          = c.Tags,
            LastOrderName = c.LastOrderName,
            City          = c.DefaultAddress?.City,
            Province      = c.DefaultAddress?.Province,
            CreatedAt     = c.CreatedAt.UtcDateTime,
        };
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            ShopifyOrderListResult result = await _client.GetOrdersAsync(limit: 1);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static ShopifyOrderResponse MapOrder(ShopifyApiOrder o)
    {
        string? trackingNumber  = o.Fulfillments.FirstOrDefault()?.TrackingNumber;
        string? trackingCompany = o.Fulfillments.FirstOrDefault()?.TrackingCompany;
        string? trackingUrl     = o.Fulfillments.FirstOrDefault()?.TrackingUrl;

        string? customerName = o.Customer is null
            ? o.ShippingAddress is null
                ? null
                : $"{o.ShippingAddress.FirstName} {o.ShippingAddress.LastName}".Trim()
            : $"{o.Customer.FirstName} {o.Customer.LastName}".Trim();

        // Extract DNI/RUC: stored in address.company by Peruvian stores (e.g. "40847172")
        string? customerDocument = ExtractDocument(o.ShippingAddress?.Company)
            ?? ExtractDocument(o.BillingAddress?.Company)
            ?? o.Customer?.Phone; // fallback

        decimal subtotal       = ParseDecimal(o.SubtotalPrice);
        decimal totalDiscounts = ParseDecimal(o.TotalDiscounts);

        var shippingLines = o.ShippingLines.Select(sl => new ShopifyShippingLineResponse
        {
            Title           = sl.Title,
            Price           = ParseDecimal(sl.Price),
            DiscountedPrice = ParseDecimal(sl.DiscountedPrice),
        }).ToList();

        bool hasFreeShipping = shippingLines.Count > 0 && shippingLines.All(sl => sl.IsFree);
        bool isLocalPickup   = shippingLines.Any(sl =>
            sl.Title.Contains("pickup", StringComparison.OrdinalIgnoreCase) ||
            sl.Title.Contains("recojo", StringComparison.OrdinalIgnoreCase) ||
            sl.Title.Contains("recog",  StringComparison.OrdinalIgnoreCase)) ||
            (shippingLines.Count == 0 && o.FulfillmentStatus == null &&
             (o.Tags ?? "").Contains("local", StringComparison.OrdinalIgnoreCase));

        return new ShopifyOrderResponse
        {
            Id                = o.Id,
            OrderNumber       = o.Name,
            CreatedAt         = o.CreatedAt.UtcDateTime,
            UpdatedAt         = o.UpdatedAt.UtcDateTime,
            FinancialStatus   = o.FinancialStatus,
            FulfillmentStatus = o.FulfillmentStatus,
            TotalPrice        = ParseDecimal(o.TotalPrice),
            Currency          = o.Currency,
            CustomerName      = string.IsNullOrWhiteSpace(customerName) ? null : customerName,
            CustomerEmail     = o.Customer?.Email,
            CustomerPhone     = o.Customer?.Phone ?? o.ShippingAddress?.Phone,
            CustomerDocument  = string.IsNullOrWhiteSpace(customerDocument) ? null : customerDocument,
            ShippingAddress   = o.ShippingAddress?.Address1,
            Province          = o.ShippingAddress?.Province,
            City              = o.ShippingAddress?.City,
            TrackingNumber    = trackingNumber,
            TrackingCompany   = trackingCompany,
            TrackingUrl       = trackingUrl,
            Note              = o.Note,
            Tags              = o.Tags,
            CancelReason      = o.CancelReason,
            IsCancelled       = o.CancelledAt.HasValue,
            SubtotalPrice     = subtotal,
            TotalDiscounts    = totalDiscounts,
            HasFreeShipping   = hasFreeShipping,
            IsLocalPickup     = isLocalPickup,
            DiscountCodes     = o.DiscountCodes.Select(dc => new ShopifyDiscountCodeResponse
            {
                Code   = dc.Code,
                Amount = ParseDecimal(dc.Amount),
                Type   = dc.Type,
            }).ToList(),
            ShippingLines     = shippingLines,
            Items = o.LineItems.Select(li => new ShopifyOrderItemResponse
            {
                Id                = li.Id,
                Title             = li.Title,
                VariantTitle      = li.VariantTitle,
                Quantity          = li.Quantity,
                Price             = ParseDecimal(li.Price),
                Sku               = li.Sku,
                FulfillmentStatus = li.FulfillmentStatus,
            }).ToList(),
        };
    }

    private static decimal ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0m;
        return decimal.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out decimal d) ? d : 0m;
    }

    /// <summary>
    /// Tries to extract a DNI (8 digits) or RUC (11 digits) from a string.
    /// Shopify stores in Peru commonly store the document number in the company field.
    /// </summary>
    private static string? ExtractDocument(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string trimmed = value.Trim();
        // DNI = 8 digits, RUC = 11 digits
        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d{8}$")
         || System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d{11}$"))
            return trimmed;
        return null;
    }
}
