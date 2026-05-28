using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AdminHiitop.Api.Application.DTOs.Shopify;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AdminHiitop.Api.Infrastructure.Shopify;

public sealed class ShopifyAdminClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient           _http;
    private readonly ShopifyOptions       _opts;
    private readonly AdminHiitopDbContext _db;

    public ShopifyAdminClient(HttpClient http, IOptions<ShopifyOptions> opts, AdminHiitopDbContext db)
    {
        _http = http;
        _opts = opts.Value;
        _db   = db;
    }

    // ── Token resolution ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns the access token to use for API calls.
    /// Priority:
    ///   1. AccessToken (shpat_...) from config → permanent, never expires, skips OAuth.
    ///   2. OAuth token stored in DB (shopify_store_connections) — installed via /oauth/install flow.
    /// </summary>
    private async Task<string> GetAccessTokenAsync()
    {
        // Priority 1: static permanent token from appsettings.json
        if (!string.IsNullOrWhiteSpace(_opts.AccessToken))
            return _opts.AccessToken.Trim();

        // Priority 2: OAuth token persisted in DB after install flow
        string shop = _opts.ShopDomain.Trim();
        if (!string.IsNullOrWhiteSpace(shop))
        {
            string? dbToken = await _db.ShopifyStoreConnections
                .Where(c => c.ShopDomain == shop && c.AccessToken != "")
                .Select(c => c.AccessToken)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(dbToken))
                return dbToken;
        }

        throw new AppException(
            "No hay token de Shopify configurado. " +
            "Agrega AccessToken en appsettings.json o instala la app via /api/shopify/oauth/install.",
            502);
    }

    // ── Orders ────────────────────────────────────────────────────────────────

    public async Task<ShopifyOrderListResult> GetOrdersAsync(
        int     limit             = 50,
        string? pageInfo          = null,
        string? financialStatus   = null,
        string? fulfillmentStatus = null,
        string? createdAtMin      = null,
        string? createdAtMax      = null,
        string? search            = null)
    {
        // When searching by non-email, fetch 250 so client-side filter has a larger pool
        int effectiveLimit = !string.IsNullOrWhiteSpace(search) && !search.Contains('@')
            ? 250
            : limit;

        string baseUrl = BuildUrl("orders.json");
        var qs = new List<string> { $"limit={effectiveLimit}", "status=any" };

        if (!string.IsNullOrWhiteSpace(pageInfo))
        {
            qs = [$"limit={limit}", $"page_info={pageInfo}"];
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(financialStatus))   qs.Add($"financial_status={financialStatus}");
            if (!string.IsNullOrWhiteSpace(fulfillmentStatus)) qs.Add($"fulfillment_status={fulfillmentStatus}");
            if (!string.IsNullOrWhiteSpace(createdAtMin))      qs.Add($"created_at_min={Uri.EscapeDataString(createdAtMin)}");
            if (!string.IsNullOrWhiteSpace(createdAtMax))      qs.Add($"created_at_max={Uri.EscapeDataString(createdAtMax)}");
            // Shopify supports email-based search in orders.json
            if (!string.IsNullOrWhiteSpace(search) && search.Contains('@'))
                qs.Add($"email={Uri.EscapeDataString(search.Trim())}");
        }

        string url = $"{baseUrl}?{string.Join("&", qs)}";
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, url);

        string? nextPageInfo = ExtractPageInfo(response, "next");
        string? prevPageInfo = ExtractPageInfo(response, "previous");

        string body = await response.Content.ReadAsStringAsync();
        EnsureSuccessOrThrow(response, body);

        using JsonDocument doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("orders", out JsonElement ordersEl))
            return new ShopifyOrderListResult();

        List<ShopifyApiOrder> orders = ordersEl.Deserialize<List<ShopifyApiOrder>>(JsonOpts) ?? [];

        // Client-side text filter for non-email searches (order number, customer name)
        if (!string.IsNullOrWhiteSpace(search) && !search.Contains('@'))
        {
            string q = search.Trim().ToLowerInvariant();
            orders = orders.Where(o =>
                (o.Name ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                || (o.Customer?.FirstName ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                || (o.Customer?.LastName  ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                || (o.Customer?.Email     ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                || (o.Customer?.Phone     ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                || (o.ShippingAddress?.Company ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        return new ShopifyOrderListResult
        {
            Orders       = orders,
            NextPageInfo = nextPageInfo,
            PrevPageInfo = prevPageInfo,
        };
    }

    public async Task<ShopifyApiOrder?> GetOrderAsync(long orderId)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, BuildUrl($"orders/{orderId}.json"));
        string body = await response.Content.ReadAsStringAsync();
        EnsureSuccessOrThrow(response, body);

        using JsonDocument doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("order", out JsonElement orderEl)
            ? orderEl.Deserialize<ShopifyApiOrder>(JsonOpts)
            : null;
    }

    // ── Fulfillment ───────────────────────────────────────────────────────────

    public async Task<bool> FulfillOrderAsync(long orderId, string? trackingNumber = null, string? trackingCompany = null)
    {
        string foUrl = BuildUrl($"orders/{orderId}/fulfillment_orders.json");
        using HttpResponseMessage foResp = await SendAsync(HttpMethod.Get, foUrl);
        string foBody = await foResp.Content.ReadAsStringAsync();
        EnsureSuccessOrThrow(foResp, foBody);

        using JsonDocument foDoc = JsonDocument.Parse(foBody);
        if (!foDoc.RootElement.TryGetProperty("fulfillment_orders", out JsonElement foEl))
            return false;

        var openIds = foEl.EnumerateArray()
            .Where(fo => fo.TryGetProperty("status", out JsonElement st) && st.GetString() is "open" or "in_progress")
            .Select(fo => fo.GetProperty("id").GetInt64())
            .ToList();

        if (openIds.Count == 0) return false;

        foreach (long foId in openIds)
        {
            object payload = !string.IsNullOrWhiteSpace(trackingNumber)
                ? new
                {
                    fulfillment = new
                    {
                        line_items_by_fulfillment_order = new[] { new { fulfillment_order_id = foId } },
                        tracking_info = new { number = trackingNumber, company = trackingCompany ?? "" },
                        notify_customer = true,
                    }
                }
                : new
                {
                    fulfillment = new
                    {
                        line_items_by_fulfillment_order = new[] { new { fulfillment_order_id = foId } },
                        notify_customer = true,
                    }
                };

            using HttpResponseMessage fr = await SendAsync(HttpMethod.Post, BuildUrl("fulfillments.json"),
                JsonSerializer.Serialize(payload, JsonOpts));
            if (!fr.IsSuccessStatusCode) return false;
        }

        return true;
    }

    public async Task<bool> CancelOrderAsync(long orderId)
    {
        using HttpResponseMessage resp = await SendAsync(HttpMethod.Post, BuildUrl($"orders/{orderId}/cancel.json"), "{}");
        return resp.IsSuccessStatusCode;
    }

    // ── Products ──────────────────────────────────────────────────────────────

    public async Task<List<ShopifyApiProduct>> GetProductsAsync(
        string? search = null,
        int     limit  = 250,
        string  status = "active")
    {
        // NOTE: Shopify REST title filter does exact-match only.
        // We fetch all products (up to 250) and do client-side substring filtering.
        var qs = new List<string> { $"limit={Math.Min(limit, 250)}", $"status={status}" };

        string url = $"{BuildUrl("products.json")}?{string.Join("&", qs)}";
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, url);
        string body = await response.Content.ReadAsStringAsync();
        EnsureSuccessOrThrow(response, body);

        using JsonDocument doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("products", out JsonElement el)
            ? el.Deserialize<List<ShopifyApiProduct>>(JsonOpts) ?? []
            : [];
    }

    public async Task<ShopifyApiProduct?> GetProductAsync(long productId)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, BuildUrl($"products/{productId}.json"));
        string body = await response.Content.ReadAsStringAsync();
        EnsureSuccessOrThrow(response, body);
        using JsonDocument doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("product", out JsonElement el)
            ? el.Deserialize<ShopifyApiProduct>(JsonOpts)
            : null;
    }

    public async Task<ShopifyApiProduct?> UpdateProductAsync(long productId, object payload)
    {
        string json = JsonSerializer.Serialize(new { product = payload }, JsonOpts);
        using HttpResponseMessage response = await SendAsync(HttpMethod.Put, BuildUrl($"products/{productId}.json"), json);
        string body = await response.Content.ReadAsStringAsync();
        EnsureSuccessOrThrow(response, body);
        using JsonDocument doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("product", out JsonElement el)
            ? el.Deserialize<ShopifyApiProduct>(JsonOpts)
            : null;
    }

    public async Task<ShopifyApiVariant?> UpdateVariantAsync(long variantId, object payload)
    {
        string json = JsonSerializer.Serialize(new { variant = payload }, JsonOpts);
        using HttpResponseMessage response = await SendAsync(HttpMethod.Put, BuildUrl($"variants/{variantId}.json"), json);
        string body = await response.Content.ReadAsStringAsync();
        EnsureSuccessOrThrow(response, body);
        using JsonDocument doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("variant", out JsonElement el)
            ? el.Deserialize<ShopifyApiVariant>(JsonOpts)
            : null;
    }

    // ── Locations & Inventory ─────────────────────────────────────────────────

    public async Task<List<ShopifyApiLocation>> GetLocationsAsync()
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, BuildUrl("locations.json"));
        string body = await response.Content.ReadAsStringAsync();
        EnsureSuccessOrThrow(response, body);
        using JsonDocument doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("locations", out JsonElement el)
            ? el.Deserialize<List<ShopifyApiLocation>>(JsonOpts) ?? []
            : [];
    }

    public async Task<List<ShopifyApiInventoryLevel>> GetInventoryLevelsAsync(
        IEnumerable<long> inventoryItemIds,
        long?             locationId = null)
    {
        string ids = string.Join(",", inventoryItemIds);
        if (string.IsNullOrEmpty(ids)) return [];

        var qs = new List<string> { $"inventory_item_ids={ids}", "limit=250" };
        if (locationId.HasValue) qs.Add($"location_ids={locationId.Value}");

        string url = $"{BuildUrl("inventory_levels.json")}?{string.Join("&", qs)}";
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, url);
        string body = await response.Content.ReadAsStringAsync();
        EnsureSuccessOrThrow(response, body);
        using JsonDocument doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("inventory_levels", out JsonElement el)
            ? el.Deserialize<List<ShopifyApiInventoryLevel>>(JsonOpts) ?? []
            : [];
    }

    public async Task<ShopifyApiProduct?> CreateProductAsync(object payload)
    {
        string json = JsonSerializer.Serialize(new { product = payload }, JsonOpts);
        using HttpResponseMessage response = await SendAsync(HttpMethod.Post, BuildUrl("products.json"), json);
        string body = await response.Content.ReadAsStringAsync();
        EnsureSuccessOrThrow(response, body);
        using JsonDocument doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("product", out JsonElement el)
            ? el.Deserialize<ShopifyApiProduct>(JsonOpts)
            : null;
    }

    /// <summary>
    /// Sets inventory to an absolute quantity (instead of delta adjust).
    /// Requires the inventory item to be tracked.
    /// </summary>
    public async Task<bool> SetInventoryQuantityAsync(long inventoryItemId, long locationId, int available)
    {
        var payload = new { location_id = locationId, inventory_item_id = inventoryItemId, available };
        string json = JsonSerializer.Serialize(new { inventory_level = payload }, JsonOpts);
        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Post, BuildUrl("inventory_levels/set.json"), json);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Adjusts inventory by a relative delta (negative = deduct, positive = add).
    /// Called when SyncInventory = true and an order with Shopify-source items is saved.
    /// </summary>
    public async Task<bool> AdjustInventoryQuantityAsync(long inventoryItemId, long locationId, int delta)
    {
        // Shopify adjust.json expects the payload directly (no "inventory_level" wrapper).
        // set.json uses the wrapper; adjust.json does not.
        var payload = new
        {
            location_id          = locationId,
            inventory_item_id    = inventoryItemId,
            available_adjustment = delta,
        };
        string json = JsonSerializer.Serialize(payload, JsonOpts);
        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Post, BuildUrl("inventory_levels/adjust.json"), json);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // 404 means the inventory item is not connected to this location yet.
            // Connect it first, then retry the adjustment.
            await ConnectInventoryLevelAsync(inventoryItemId, locationId);

            using HttpResponseMessage retry = await SendAsync(
                HttpMethod.Post, BuildUrl("inventory_levels/adjust.json"), json);
            if (!retry.IsSuccessStatusCode)
            {
                string retryBody = await retry.Content.ReadAsStringAsync();
                EnsureSuccessOrThrow(retry, retryBody);
            }
            return true;
        }

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync();
            EnsureSuccessOrThrow(response, body);
        }
        return true;
    }

    private async Task ConnectInventoryLevelAsync(long inventoryItemId, long locationId)
    {
        var payload = new { location_id = locationId, inventory_item_id = inventoryItemId };
        string json = JsonSerializer.Serialize(new { inventory_level = payload }, JsonOpts);
        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Post, BuildUrl("inventory_levels/connect.json"), json);
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync();
            EnsureSuccessOrThrow(response, body);
        }
    }

    // ── Product images ────────────────────────────────────────────────────────

    public async Task<ShopifyApiImage?> AddProductImageAsync(long productId, string base64Data, string filename)
    {
        var payload = new
        {
            image = new
            {
                attachment = base64Data,
                filename   = filename,
            }
        };
        string json = JsonSerializer.Serialize(payload, JsonOpts);
        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Post, BuildUrl($"products/{productId}/images.json"), json);
        string body = await response.Content.ReadAsStringAsync();
        EnsureSuccessOrThrow(response, body);
        using JsonDocument doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("image", out JsonElement el)
            ? el.Deserialize<ShopifyApiImage>(JsonOpts)
            : null;
    }

    // ── All orders (multi-page) ───────────────────────────────────────────────

    /// <summary>
    /// Fetches orders from Shopify across multiple pages.
    /// Pass maxOrders = 0 to load ALL available orders (no artificial cap; stops when Shopify has no more).
    /// </summary>
    public async Task<List<ShopifyApiOrder>> GetAllOrdersAsync(
        int     maxOrders         = 250,
        string? financialStatus   = null,
        string? fulfillmentStatus = null,
        string? createdAtMin      = null,
        string? createdAtMax      = null)
    {
        bool unlimited = maxOrders <= 0;
        int  hardCap   = unlimited ? 100_000 : maxOrders;

        var all = new List<ShopifyApiOrder>();
        string? pageInfo = null;
        bool firstPage = true;

        while (all.Count < hardCap)
        {
            int batchSize = unlimited ? 250 : Math.Min(250, hardCap - all.Count);
            string baseUrl = BuildUrl("orders.json");
            List<string> qs;

            if (!firstPage && pageInfo != null)
            {
                qs = [$"limit={batchSize}", $"page_info={pageInfo}"];
            }
            else
            {
                qs = [$"limit={batchSize}", "status=any"];
                if (financialStatus   != null) qs.Add($"financial_status={financialStatus}");
                if (fulfillmentStatus != null) qs.Add($"fulfillment_status={fulfillmentStatus}");
                if (createdAtMin      != null) qs.Add($"created_at_min={Uri.EscapeDataString(createdAtMin)}");
                if (createdAtMax      != null) qs.Add($"created_at_max={Uri.EscapeDataString(createdAtMax)}");
            }

            string url = $"{baseUrl}?{string.Join("&", qs)}";
            using HttpResponseMessage response = await SendAsync(HttpMethod.Get, url);
            string nextCursor = ExtractPageInfo(response, "next") ?? "";
            string body = await response.Content.ReadAsStringAsync();
            EnsureSuccessOrThrow(response, body);

            using JsonDocument doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("orders", out JsonElement ordersEl)) break;

            List<ShopifyApiOrder> batch = ordersEl.Deserialize<List<ShopifyApiOrder>>(JsonOpts) ?? [];
            all.AddRange(batch);

            pageInfo = string.IsNullOrEmpty(nextCursor) ? null : nextCursor;
            firstPage = false;
            if (pageInfo is null || batch.Count == 0) break;
        }

        return unlimited ? all : all.Take(hardCap).ToList();
    }

    // ── Customers ─────────────────────────────────────────────────────────────

    public async Task<(List<ShopifyApiCustomer> Customers, string? NextPageInfo)> GetCustomersAsync(
        string? search   = null,
        int     limit    = 50,
        string? pageInfo = null)
    {
        var qs = new List<string> { $"limit={Math.Min(limit, 250)}" };
        if (!string.IsNullOrWhiteSpace(pageInfo))
        {
            qs = [$"limit={limit}", $"page_info={pageInfo}"];
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(search)) qs.Add($"query={Uri.EscapeDataString(search.Trim())}");
        }

        string url = $"{BuildUrl("customers.json")}?{string.Join("&", qs)}";
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, url);
        string next = ExtractPageInfo(response, "next") ?? "";
        string body = await response.Content.ReadAsStringAsync();
        EnsureSuccessOrThrow(response, body);

        using JsonDocument doc = JsonDocument.Parse(body);
        List<ShopifyApiCustomer> customers = doc.RootElement.TryGetProperty("customers", out JsonElement el)
            ? el.Deserialize<List<ShopifyApiCustomer>>(JsonOpts) ?? []
            : [];

        return (customers, string.IsNullOrEmpty(next) ? null : next);
    }

    // ── Collections ───────────────────────────────────────────────────────────

    public async Task<List<ShopifyApiCollection>> GetCustomCollectionsAsync()
    {
        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Get, $"{BuildUrl("custom_collections.json")}?limit=250");
        string body = await response.Content.ReadAsStringAsync();
        EnsureSuccessOrThrow(response, body);
        using JsonDocument doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("custom_collections", out JsonElement el)
            ? el.Deserialize<List<ShopifyApiCollection>>(JsonOpts) ?? []
            : [];
    }

    public async Task<List<ShopifyApiCollection>> GetSmartCollectionsAsync()
    {
        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Get, $"{BuildUrl("smart_collections.json")}?limit=250");
        string body = await response.Content.ReadAsStringAsync();
        EnsureSuccessOrThrow(response, body);
        using JsonDocument doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("smart_collections", out JsonElement el)
            ? el.Deserialize<List<ShopifyApiCollection>>(JsonOpts) ?? []
            : [];
    }

    public async Task<List<ShopifyApiCollect>> GetProductCollectsAsync(long productId)
    {
        string url = $"{BuildUrl("collects.json")}?product_id={productId}&limit=250";
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, url);
        string body = await response.Content.ReadAsStringAsync();
        EnsureSuccessOrThrow(response, body);
        using JsonDocument doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("collects", out JsonElement el)
            ? el.Deserialize<List<ShopifyApiCollect>>(JsonOpts) ?? []
            : [];
    }

    public async Task<bool> AddCollectAsync(long productId, long collectionId)
    {
        var payload = new { collect = new { product_id = productId, collection_id = collectionId } };
        string json = JsonSerializer.Serialize(payload, JsonOpts);
        using HttpResponseMessage response = await SendAsync(HttpMethod.Post, BuildUrl("collects.json"), json);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RemoveCollectAsync(long collectId)
    {
        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Delete, BuildUrl($"collects/{collectId}.json"));
        return response.IsSuccessStatusCode;
    }

    // ── GraphQL search ────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions GqlReadOpts = new() { PropertyNameCaseInsensitive = true };

    // Orders query: lineItems(first:10) + shippingLines(first:3) keeps query cost ~560 pts
    private const string OrdersGqlQuery = """
        query SearchOrders($query: String!, $first: Int!, $after: String) {
          orders(first: $first, after: $after, query: $query) {
            edges {
              node {
                id name createdAt updatedAt currencyCode
                displayFinancialStatus displayFulfillmentStatus
                totalPriceSet       { shopMoney { amount currencyCode } }
                subtotalPriceSet    { shopMoney { amount } }
                totalDiscountsSet   { shopMoney { amount } }
                customer            { firstName lastName email phone }
                shippingAddress     { firstName lastName company address1 city province phone }
                billingAddress      { company }
                note tags cancelReason cancelledAt
                fulfillments        { trackingInfo { number company url } }
                lineItems(first: 10) {
                  edges { node {
                    id title variantTitle quantity sku fulfillmentStatus
                    originalUnitPriceSet { shopMoney { amount } }
                  }}
                }
                shippingLines(first: 3) {
                  edges { node {
                    title
                    originalPriceSet   { shopMoney { amount } }
                    discountedPriceSet { shopMoney { amount } }
                  }}
                }
                discountCodes
              }
            }
            pageInfo { hasNextPage endCursor }
          }
        }
        """;

    // Products query: variants(first:50) — keeps cost ~510 pts for 10 products
    private const string ProductsGqlQuery = """
        query SearchProducts($query: String!, $first: Int!) {
          products(first: $first, query: $query) {
            edges {
              node {
                id title descriptionHtml status handle productType tags vendor
                featuredImage { url altText }
                images(first: 3) { edges { node { url altText } } }
                options { id name position values }
                variants(first: 50) {
                  edges { node {
                    id title sku price compareAtPrice position inventoryQuantity
                    selectedOptions { name value }
                    inventoryItem { id }
                  }}
                }
              }
            }
            pageInfo { hasNextPage endCursor }
          }
        }
        """;

    /// <summary>
    /// Single-page GraphQL order search. Returns up to 50 results with a cursor for next page.
    /// Use for the paginated orders view when a search term is present.
    /// </summary>
    public async Task<ShopifyOrderListResult> SearchOrdersGraphQlAsync(
        string  gqlQuery,
        int     limit = 50,
        string? after = null)
    {
        string json = JsonSerializer.Serialize(
            new { query = OrdersGqlQuery, variables = new { query = gqlQuery, first = Math.Clamp(limit, 1, 50), after } },
            JsonOpts);

        using HttpResponseMessage response = await SendAsync(HttpMethod.Post, BuildGraphQlUrl(), json);
        string body = await response.Content.ReadAsStringAsync();
        EnsureSuccessOrThrow(response, body);

        var resp = JsonSerializer.Deserialize<ShopifyGqlResponse<ShopifyGqlOrdersData>>(body, GqlReadOpts);
        if (resp?.Errors?.Count > 0)
            throw new AppException($"Shopify GraphQL: {resp.Errors[0].Message}");

        var conn = resp?.Data?.Orders ?? new ShopifyGqlConnection<ShopifyGqlOrder>();
        return new ShopifyOrderListResult
        {
            Orders       = conn.Edges.Select(e => MapGqlOrder(e.Node)).ToList(),
            NextPageInfo = conn.PageInfo.HasNextPage ? conn.PageInfo.EndCursor : null,
            PrevPageInfo = null,
        };
    }

    /// <summary>
    /// Paginates ALL GraphQL results matching gqlQuery (up to hardCap).
    /// Dramatically faster than GetAllOrdersAsync + client-side filter because only matching
    /// orders are transferred — the search runs on Shopify's server-side index.
    /// </summary>
    public async Task<List<ShopifyApiOrder>> GetAllMatchingOrdersGraphQlAsync(
        string gqlQuery,
        int    hardCap = 10_000)
    {
        var    all   = new List<ShopifyApiOrder>();
        string? after = null;
        string  url  = BuildGraphQlUrl();

        while (all.Count < hardCap)
        {
            int batchSize = Math.Min(50, hardCap - all.Count);
            string json = JsonSerializer.Serialize(
                new { query = OrdersGqlQuery, variables = new { query = gqlQuery, first = batchSize, after } },
                JsonOpts);

            using HttpResponseMessage response = await SendAsync(HttpMethod.Post, url, json);
            string body = await response.Content.ReadAsStringAsync();
            EnsureSuccessOrThrow(response, body);

            var resp = JsonSerializer.Deserialize<ShopifyGqlResponse<ShopifyGqlOrdersData>>(body, GqlReadOpts);
            if (resp?.Errors?.Count > 0)
                throw new AppException($"Shopify GraphQL: {resp.Errors[0].Message}");

            var conn = resp?.Data?.Orders ?? new ShopifyGqlConnection<ShopifyGqlOrder>();
            all.AddRange(conn.Edges.Select(e => MapGqlOrder(e.Node)));

            if (!conn.PageInfo.HasNextPage) break;
            after = conn.PageInfo.EndCursor;
        }

        return all;
    }

    /// <summary>
    /// Lightweight GraphQL product search for the product LIST view.
    /// No variants fetched — uses priceRangeV2/totalInventory/variantsCount scalars.
    /// Returns up to 50 products; query cost is ~50-100 points vs 510+ for the variant query.
    /// </summary>
    private const string ProductsLiteGqlQuery = """
        query SearchProductsLite($query: String!, $first: Int!) {
          products(first: $first, query: $query) {
            edges {
              node {
                id title status productType tags vendor
                featuredImage { url }
                variantsCount { count }
                priceRangeV2 {
                  minVariantPrice { amount }
                  maxVariantPrice { amount }
                }
                totalInventory
                variants(first: 50) {
                  edges { node { inventoryItem { id } } }
                }
              }
            }
            pageInfo { hasNextPage endCursor }
          }
        }
        """;

    public async Task<List<ShopifyGqlProductLite>> SearchProductsLiteGraphQlAsync(string gqlQuery, int limit = 50)
    {
        string json = JsonSerializer.Serialize(
            new { query = ProductsLiteGqlQuery, variables = new { query = gqlQuery, first = Math.Clamp(limit, 1, 50) } },
            JsonOpts);

        using HttpResponseMessage response = await SendAsync(HttpMethod.Post, BuildGraphQlUrl(), json);
        string body = await response.Content.ReadAsStringAsync();
        EnsureSuccessOrThrow(response, body);

        var resp = JsonSerializer.Deserialize<ShopifyGqlResponse<ShopifyGqlProductsLiteData>>(body, GqlReadOpts);
        if (resp?.Errors?.Count > 0)
            throw new AppException($"Shopify GraphQL: {resp.Errors[0].Message}");

        var conn = resp?.Data?.Products ?? new ShopifyGqlConnection<ShopifyGqlProductLite>();
        return conn.Edges.Select(e => e.Node).ToList();
    }

    /// <summary>
    /// GraphQL product search — uses Shopify's server-side index (title, SKU, tags, vendor, type).
    /// Returns up to 10 products with variants already mapped to ShopifyApiProduct.
    /// </summary>
    public async Task<List<ShopifyApiProduct>> SearchProductsGraphQlAsync(string gqlQuery, int limit = 10)
    {
        string json = JsonSerializer.Serialize(
            new { query = ProductsGqlQuery, variables = new { query = gqlQuery, first = Math.Clamp(limit, 1, 10) } },
            JsonOpts);

        using HttpResponseMessage response = await SendAsync(HttpMethod.Post, BuildGraphQlUrl(), json);
        string body = await response.Content.ReadAsStringAsync();
        EnsureSuccessOrThrow(response, body);

        var resp = JsonSerializer.Deserialize<ShopifyGqlResponse<ShopifyGqlProductsData>>(body, GqlReadOpts);
        if (resp?.Errors?.Count > 0)
            throw new AppException($"Shopify GraphQL: {resp.Errors[0].Message}");

        var conn = resp?.Data?.Products ?? new ShopifyGqlConnection<ShopifyGqlProduct>();
        return conn.Edges.Select(e => MapGqlProduct(e.Node)).ToList();
    }

    private string BuildGraphQlUrl()
        => $"https://{_opts.ShopDomain.Trim().TrimEnd('/')}/admin/api/{_opts.ApiVersion.Trim()}/graphql.json";

    private static ShopifyApiOrder MapGqlOrder(ShopifyGqlOrder o) => new()
    {
        Id                = ExtractGqlId(o.Id),
        Name              = o.Name,
        CreatedAt         = o.CreatedAt,
        UpdatedAt         = o.UpdatedAt,
        FinancialStatus   = o.DisplayFinancialStatus?.ToLowerInvariant(),
        FulfillmentStatus = o.DisplayFulfillmentStatus switch
        {
            "UNFULFILLED" or null => null,
            "PARTIALLY_FULFILLED" => "partial",
            var s                 => s.ToLowerInvariant(),
        },
        TotalPrice     = o.TotalPriceSet?.ShopMoney.Amount ?? "0.00",
        SubtotalPrice  = o.SubtotalPriceSet?.ShopMoney.Amount ?? "0.00",
        TotalDiscounts = o.TotalDiscountsSet?.ShopMoney.Amount ?? "0.00",
        Currency       = o.TotalPriceSet?.ShopMoney.CurrencyCode ?? "PEN",
        Tags           = o.Tags.Count > 0 ? string.Join(",", o.Tags) : null,
        Note           = o.Note,
        CancelReason   = o.CancelReason?.ToLowerInvariant(),
        CancelledAt    = o.CancelledAt,
        Customer = o.Customer is null ? null : new ShopifyApiCustomer
        {
            FirstName = o.Customer.FirstName,
            LastName  = o.Customer.LastName,
            Email     = o.Customer.Email,
            Phone     = o.Customer.Phone,
        },
        ShippingAddress = o.ShippingAddress is null ? null : new ShopifyApiAddress
        {
            FirstName = o.ShippingAddress.FirstName,
            LastName  = o.ShippingAddress.LastName,
            Company   = o.ShippingAddress.Company,
            Address1  = o.ShippingAddress.Address1,
            City      = o.ShippingAddress.City,
            Province  = o.ShippingAddress.Province,
            Phone     = o.ShippingAddress.Phone,
        },
        BillingAddress = o.BillingAddress is null ? null : new ShopifyApiAddress
        {
            Company = o.BillingAddress.Company,
        },
        Fulfillments = o.Fulfillments.Select(f => {
            var info = f.TrackingInfo.FirstOrDefault();
            return new ShopifyApiFulfillment
            {
                TrackingNumber  = info?.Number,
                TrackingCompany = info?.Company,
                TrackingUrl     = info?.Url,
            };
        }).ToList(),
        LineItems = o.LineItems.Edges.Select(e => new ShopifyApiLineItem
        {
            Id                = ExtractGqlId(e.Node.Id),
            Title             = e.Node.Title,
            VariantTitle      = e.Node.VariantTitle,
            Quantity          = e.Node.Quantity,
            Price             = e.Node.OriginalUnitPriceSet?.ShopMoney.Amount ?? "0.00",
            Sku               = e.Node.Sku,
            FulfillmentStatus = e.Node.FulfillmentStatus,
        }).ToList(),
        ShippingLines = o.ShippingLines.Edges.Select(e => new ShopifyApiShippingLine
        {
            Title           = e.Node.Title,
            Price           = e.Node.OriginalPriceSet?.ShopMoney.Amount ?? "0.00",
            DiscountedPrice = e.Node.DiscountedPriceSet?.ShopMoney.Amount ?? "0.00",
        }).ToList(),
        DiscountCodes = o.DiscountCodes.Select(code => new ShopifyApiDiscountCode { Code = code }).ToList(),
    };

    private static ShopifyApiProduct MapGqlProduct(ShopifyGqlProduct p)
    {
        long numericId = ExtractGqlId(p.Id);

        var images = p.Images.Edges
            .Select(e => new ShopifyApiImage { Src = e.Node.Url, Alt = e.Node.AltText })
            .ToList();

        ShopifyApiImage? mainImage = p.FeaturedImage is null
            ? images.FirstOrDefault()
            : new ShopifyApiImage { Src = p.FeaturedImage.Url, Alt = p.FeaturedImage.AltText };

        return new ShopifyApiProduct
        {
            Id          = numericId,
            Title       = p.Title,
            BodyHtml    = p.DescriptionHtml,
            Status      = p.Status.ToLowerInvariant(),
            Handle      = p.Handle,
            ProductType = p.ProductType,
            Tags        = p.Tags.Count > 0 ? string.Join(",", p.Tags) : null,
            Vendor      = p.Vendor,
            Image       = mainImage,
            Images      = images,
            Options = p.Options.Select(o => new ShopifyApiProductOption
            {
                Id       = ExtractGqlId(o.Id),
                Name     = o.Name,
                Position = o.Position,
                Values   = o.Values,
            }).ToList(),
            Variants = p.Variants.Edges.Select(e =>
            {
                var v = e.Node;
                return new ShopifyApiVariant
                {
                    Id                  = ExtractGqlId(v.Id),
                    ProductId           = numericId,
                    Title               = v.Title,
                    Sku                 = v.Sku,
                    Price               = v.Price,
                    CompareAtPrice      = v.CompareAtPrice,
                    Option1             = v.SelectedOptions.Count > 0 ? v.SelectedOptions[0].Value : null,
                    Option2             = v.SelectedOptions.Count > 1 ? v.SelectedOptions[1].Value : null,
                    Option3             = v.SelectedOptions.Count > 2 ? v.SelectedOptions[2].Value : null,
                    InventoryItemId     = v.InventoryItem is null ? 0 : ExtractGqlId(v.InventoryItem.Id),
                    InventoryQuantity   = v.InventoryQuantity,
                    InventoryManagement = "shopify",
                    Position            = v.Position,
                };
            }).ToList(),
        };
    }

    // Extracts numeric ID from Shopify GID: "gid://shopify/Order/1234567890" → 1234567890
    private static long ExtractGqlId(string gid)
    {
        int slash = gid.LastIndexOf('/');
        return slash >= 0 && long.TryParse(gid[(slash + 1)..], out long id) ? id : 0;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string BuildUrl(string path)
        => $"https://{_opts.ShopDomain.Trim().TrimEnd('/')}/admin/api/{_opts.ApiVersion.Trim()}/{path}";

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? jsonBody = null)
    {
        if (string.IsNullOrWhiteSpace(_opts.ShopDomain))
            throw new AppException("Shopify ShopDomain no está configurado.");

        string token = await GetAccessTokenAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(_opts.TimeoutSeconds, 10)));
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-Shopify-Access-Token", token);

        if (jsonBody is not null)
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        return await _http.SendAsync(request, cts.Token);
    }

    private static void EnsureSuccessOrThrow(HttpResponseMessage response, string body)
    {
        if (response.IsSuccessStatusCode) return;

        int status = (int)response.StatusCode;
        string shopifyDetail = ExtractShopifyError(body);

        // Map Shopify auth/scope errors to 502 so our API doesn't emit 401/403
        // (which would look like a failed login on the .NET side).
        string message = status switch
        {
            401 => $"Shopify rechazó el access token (401). Verifica que el token en appsettings.json sea válido. Detalle: {shopifyDetail}",
            403 => $"Permisos insuficientes en Shopify (403). Asegúrate de que el token tenga los scopes: read_products, write_products, read_inventory, write_inventory, read_orders. Detalle: {shopifyDetail}",
            429 => $"Límite de tasa de Shopify alcanzado (429). Reintenta en unos segundos. Detalle: {shopifyDetail}",
            _   => $"Shopify API {status}: {shopifyDetail}",
        };

        // Use 502 for auth/scope errors so they don't masquerade as .NET API auth failures
        int outboundStatus = status is 401 or 403 ? 502 : status;
        throw new AppException(message, outboundStatus);
    }

    private static string ExtractShopifyError(string body)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("errors", out JsonElement errEl))
                return errEl.ValueKind == JsonValueKind.String ? errEl.GetString() ?? body : errEl.GetRawText();
        }
        catch { /* ignore */ }
        return body;
    }

    private static string? ExtractPageInfo(HttpResponseMessage response, string rel)
    {
        if (!response.Headers.TryGetValues("Link", out IEnumerable<string>? linkValues)) return null;
        string linkHeader = string.Join(", ", linkValues);
        var match = Regex.Match(linkHeader, $@"page_info=([^&>]+)[^>]*>;\s*rel=""{rel}""");
        return match.Success ? match.Groups[1].Value : null;
    }
}
