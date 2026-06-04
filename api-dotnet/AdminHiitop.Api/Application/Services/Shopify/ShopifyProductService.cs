using AdminHiitop.Api.Application.DTOs.Shopify;
using AdminHiitop.Api.Application.DTOs.Stocks;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Shopify.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Infrastructure.Shopify;
using AdminHiitop.Api.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AdminHiitop.Api.Application.Services.Shopify;

public sealed class ShopifyProductService : IShopifyProductService
{
    private readonly ShopifyAdminClient    _client;
    private readonly ShopifyOptions        _opts;
    private readonly AdminHiitopDbContext  _db;

    // Cached active location — resolved once per service lifetime (scoped)
    private long? _resolvedLocationId;

    public ShopifyProductService(
        ShopifyAdminClient   client,
        IOptions<ShopifyOptions> opts,
        AdminHiitopDbContext db)
    {
        _client = client;
        _opts   = opts.Value;
        _db     = db;
    }

    // ── Lookup (same shape as /api/stocks/lookup) ─────────────────────────────

    public async Task<IReadOnlyList<StockLookupResponse>> GetLookupAsync(string? search, int limit, long? locationId = null)
    {
        long resolvedLocationId = locationId > 0 ? locationId.Value : await ResolveLocationIdAsync();

        List<ShopifyApiProduct> products;
        if (!string.IsNullOrWhiteSpace(search))
        {
            // GraphQL: server-side search across title, SKU, tags, vendor, product type
            products = await _client.SearchProductsGraphQlAsync(BuildProductGqlQuery(search, "active"), 10);
        }
        else
        {
            List<ShopifyApiProduct> allProducts = await _client.GetProductsAsync(null, 250);
            products = allProducts.Take(Math.Min(limit, 250)).ToList();
        }
        if (products.Count == 0) return [];

        // Fetch inventory levels for all variants in one call
        var inventoryItemIds = products
            .SelectMany(p => p.Variants)
            .Select(v => v.InventoryItemId)
            .Distinct()
            .ToList();

        List<ShopifyApiInventoryLevel> levels = await _client.GetInventoryLevelsAsync(inventoryItemIds, resolvedLocationId);
        var levelMap = levels.ToDictionary(l => l.InventoryItemId, l => l.Available ?? 0);

        var result = new List<StockLookupResponse>();

        foreach (ShopifyApiProduct product in products)
        {
            string? imageUrl = product.Image?.Src ?? product.Images.FirstOrDefault()?.Src;
            (int colorPos, int sizePos) = DetectOptionPositions(product);

            foreach (ShopifyApiVariant variant in product.Variants)
            {
                int qty = levelMap.GetValueOrDefault(variant.InventoryItemId, 0);

                string? colorName = GetOption(variant, colorPos);
                string? size      = GetOption(variant, sizePos);

                // Build a readable label: "Rojo / M [SKU-001]"
                string variantPart = variant.Title == "Default Title" ? "" : variant.Title;
                string skuPart     = string.IsNullOrWhiteSpace(variant.Sku) ? "" : $" [{variant.Sku}]";
                string label       = string.IsNullOrWhiteSpace(variantPart)
                    ? product.Title + skuPart
                    : $"{product.Title} — {variantPart}{skuPart}";

                result.Add(new StockLookupResponse
                {
                    StockId          = 0,
                    ProductId        = 0,
                    ProductName      = product.Title,
                    Sku              = string.IsNullOrWhiteSpace(variant.Sku) ? null : variant.Sku,
                    WarehouseId      = 0,
                    WarehouseName    = "",
                    ColorId          = null,
                    ColorName        = colorName,
                    Size             = size,
                    AvailableQty     = qty,
                    UnitPrice        = ParseDecimal(variant.Price),
                    UnitCost         = 0m,
                    VariantLabel     = label,
                    Source                  = "shopify",
                    ShopifyVariantId        = variant.Id,
                    ShopifyProductId        = product.Id,
                    ShopifyLocationId       = resolvedLocationId,
                    ShopifyInventoryItemId  = variant.InventoryItemId,
                    ImageUrl                = imageUrl,
                });
            }
        }

        return result.Take(limit).ToList();
    }

    // ── Product list ──────────────────────────────────────────────────────────

    public async Task<ShopifyProductListResponse> GetProductsAsync(
        string? search, int page, int perPage, string status = "active", long? locationId = null)
    {
        long resolvedLocationId = locationId > 0 ? locationId.Value : await ResolveLocationIdAsync();

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Lite GraphQL: server-side full-text, returns up to 50 products
            string gqlQuery = BuildProductGqlQuery(search, status);
            List<ShopifyGqlProductLite> liteAll = await _client.SearchProductsLiteGraphQlAsync(gqlQuery, 50);

            // Fetch per-location inventory for the paged slice
            var pagedLite = liteAll.Skip((page - 1) * perPage).Take(perPage).ToList();
            var liteItemIds = pagedLite
                .SelectMany(p => p.Variants.Edges)
                .Select(e => ParseGidLong(e.Node.InventoryItem?.Id))
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            var liteLevelMap = liteItemIds.Count > 0
                ? (await _client.GetInventoryLevelsAsync(liteItemIds, resolvedLocationId))
                    .ToDictionary(l => l.InventoryItemId, l => l.Available ?? 0)
                : new Dictionary<long, int>();

            return new ShopifyProductListResponse
            {
                Total    = liteAll.Count,
                Products = pagedLite.Select(p => MapGqlLiteSummary(p, liteLevelMap)).ToList(),
            };
        }

        // Browse without search: REST, fetch up to 250 products
        List<ShopifyApiProduct> all = await _client.GetProductsAsync(null, 250, status);

        // Pagination slice
        List<ShopifyApiProduct> paged = all
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .ToList();

        // Fetch inventory for the page using the requested location
        var inventoryItemIds = paged.SelectMany(p => p.Variants).Select(v => v.InventoryItemId).Distinct().ToList();
        var levelMap = inventoryItemIds.Count > 0
            ? (await _client.GetInventoryLevelsAsync(inventoryItemIds, resolvedLocationId))
                .ToDictionary(l => l.InventoryItemId, l => l.Available ?? 0)
            : new Dictionary<long, int>();

        return new ShopifyProductListResponse
        {
            Total    = all.Count,
            Products = paged.Select(p => MapSummary(p, levelMap)).ToList(),
        };
    }

    // ── Product detail ────────────────────────────────────────────────────────

    public async Task<ShopifyProductDetailResponse?> GetProductAsync(long productId, long? locationId = null)
    {
        ShopifyApiProduct? product = await _client.GetProductAsync(productId);
        if (product is null) return null;

        long resolvedLocationId = locationId > 0 ? locationId!.Value : await ResolveLocationIdAsync();
        var inventoryItemIds = product.Variants.Select(v => v.InventoryItemId).Distinct().ToList();
        var levelMap = inventoryItemIds.Count > 0
            ? (await _client.GetInventoryLevelsAsync(inventoryItemIds, resolvedLocationId))
                .ToDictionary(l => l.InventoryItemId, l => l.Available ?? 0)
            : new Dictionary<long, int>();

        return MapDetail(product, levelMap);
    }

    // ── Update product ────────────────────────────────────────────────────────

    public async Task<ShopifyProductDetailResponse> UpdateProductAsync(
        long productId, ShopifyProductUpdateRequest request)
    {
        // Build only the fields that were provided
        var payload = new Dictionary<string, object?>();
        if (request.Title       is not null) payload["title"]        = request.Title;
        if (request.BodyHtml    is not null) payload["body_html"]    = request.BodyHtml;
        if (request.ProductType is not null) payload["product_type"] = request.ProductType;
        if (request.Tags        is not null) payload["tags"]         = request.Tags;
        if (request.Vendor      is not null) payload["vendor"]       = request.Vendor;
        if (request.Status      is not null) payload["status"]       = request.Status;
        if (request.Images      is not null) payload["images"]       = request.Images;

        ShopifyApiProduct? updated = await _client.UpdateProductAsync(productId, payload);
        if (updated is null)
            throw new AppException("Shopify no devolvió el producto actualizado.", 502);

        return MapDetail(updated, new Dictionary<long, int>());
    }

    // ── Update variant ────────────────────────────────────────────────────────

    public async Task<ShopifyVariantResponse> UpdateVariantAsync(
        long variantId, ShopifyVariantUpdateRequest request)
    {
        var payload = new Dictionary<string, object?> { ["id"] = variantId };
        if (request.Sku                 is not null) payload["sku"]                  = request.Sku;
        if (request.Price               is not null) payload["price"]                = request.Price.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        if (request.CompareAtPrice      is not null) payload["compare_at_price"]     = request.CompareAtPrice.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        if (request.Option1             is not null) payload["option1"]              = request.Option1;
        if (request.Option2             is not null) payload["option2"]              = request.Option2;
        if (request.Option3             is not null) payload["option3"]              = request.Option3;
        if (request.InventoryManagement is not null) payload["inventory_management"] = string.IsNullOrEmpty(request.InventoryManagement) ? null : request.InventoryManagement;

        ShopifyApiVariant? updated = await _client.UpdateVariantAsync(variantId, payload);
        if (updated is null)
            throw new AppException("Shopify no devolvió la variante actualizada.", 502);

        return MapVariant(updated, 0);
    }

    // ── Locations ─────────────────────────────────────────────────────────────

    public async Task<List<ShopifyLocationResponse>> GetLocationsAsync()
    {
        List<ShopifyApiLocation> locations = await _client.GetLocationsAsync();
        return locations.Select(l => new ShopifyLocationResponse
        {
            Id      = l.Id,
            Name    = l.Name,
            Active  = l.Active,
            Address = l.Address1,
            City    = l.City,
        }).ToList();
    }

    // ── Metrics ───────────────────────────────────────────────────────────────

    // Peru is UTC-5 (no DST). Dates from the frontend are calendar dates in Peru time.
    private const int PeruOffsetHours = 5;

    public async Task<ShopifyMetricsResponse> GetMetricsAsync(DateTime? startDate, DateTime? endDate)
    {
        // Treat incoming dates as Peru calendar dates, convert to UTC for Shopify.
        // Midnight Peru = 05:00 UTC same day. End-of-day Peru = 04:59:59 UTC next day.
        string? minDate = startDate.HasValue
            ? new DateTime(startDate.Value.Year, startDate.Value.Month, startDate.Value.Day, 0, 0, 0)
                .AddHours(PeruOffsetHours)
                .ToString("yyyy-MM-ddTHH:mm:ssZ")
            : null;

        string? maxDate = endDate.HasValue
            ? new DateTime(endDate.Value.Year, endDate.Value.Month, endDate.Value.Day, 23, 59, 59)
                .AddHours(PeruOffsetHours)
                .ToString("yyyy-MM-ddTHH:mm:ssZ")
            : null;

        // Use GetAllOrdersAsync to get ALL paid orders in the period (not capped at 250)
        List<ShopifyApiOrder> allOrders = await _client.GetAllOrdersAsync(
            maxOrders:       0,  // unlimited within the date range
            financialStatus: "paid",
            createdAtMin:    minDate,
            createdAtMax:    maxDate);
        var active    = allOrders.Where(o => !o.CancelledAt.HasValue).ToList();
        var cancelled = allOrders.Where(o => o.CancelledAt.HasValue).ToList();

        decimal totalRevenue = active.Sum(o => ParseDecimal(o.TotalPrice));
        int     total        = active.Count;

        var topProducts = active
            .SelectMany(o => o.LineItems)
            .GroupBy(li => li.Title)
            .Select(g => new ShopifyTopProduct
            {
                Title    = g.Key,
                Quantity = g.Sum(li => li.Quantity),
                Revenue  = g.Sum(li => ParseDecimal(li.Price) * li.Quantity),
            })
            .OrderByDescending(x => x.Quantity)
            .Take(10)
            .ToList();

        // Group by Peru date (UTC - 5h) so orders from e.g. 04/06 04:30 UTC appear on 03/06 Peru.
        var dailyStats = active
            .GroupBy(o => o.CreatedAt.AddHours(-PeruOffsetHours).Date)
            .OrderBy(g => g.Key)
            .Select(g => new ShopifyDailyStat
            {
                Date    = g.Key.ToString("yyyy-MM-dd"),
                Orders  = g.Count(),
                Revenue = g.Sum(o => ParseDecimal(o.TotalPrice)),
            })
            .ToList();

        int fulfilled = active.Count(o =>
            string.Equals(o.FulfillmentStatus, "fulfilled", StringComparison.OrdinalIgnoreCase));
        int pending = active.Count(o => o.FulfillmentStatus is null or "");

        return new ShopifyMetricsResponse
        {
            TotalOrders       = total,
            TotalRevenue      = totalRevenue,
            AverageOrderValue = total > 0 ? Math.Round(totalRevenue / total, 2) : 0m,
            PendingOrders     = pending,
            FulfilledOrders   = fulfilled,
            CancelledOrders   = cancelled.Count,
            TopProducts       = topProducts,
            DailyStats        = dailyStats,
        };
    }

    // ── Inventory adjustment / create / bulk ──────────────────────────────────

    public async Task<bool> AdjustInventoryAsync(long inventoryItemId, long locationId, int delta)
        => await _client.AdjustInventoryQuantityAsync(inventoryItemId, locationId, delta);

    public async Task<bool> SetInventoryAsync(long inventoryItemId, long locationId, int available)
        => await _client.SetInventoryQuantityAsync(inventoryItemId, locationId, available);

    public async Task<ShopifyProductDetailResponse> CreateProductAsync(
        ShopifyProductCreateRequest request, long? locationId = null)
    {
        long effectiveLocationId = locationId ?? await ResolveLocationIdAsync();

        // Build Shopify product payload
        var options = request.Options.Count > 0
            ? request.Options.Select((name, i) => new { name, position = i + 1 }).ToList<object>()
            : new List<object> { new { name = "Talla" } };

        var variants = request.Variants.Select(v =>
        {
            var vd = new Dictionary<string, object?>
            {
                ["option1"] = v.Option1,
                ["option2"] = v.Option2,
                ["option3"] = v.Option3,
                ["sku"]     = v.Sku,
                ["barcode"] = v.Barcode,
                ["price"]   = v.Price.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                ["inventory_management"] = "shopify",
            };
            if (v.CompareAtPrice.HasValue)
                vd["compare_at_price"] = v.CompareAtPrice.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            return (object)vd;
        }).ToList();

        var payloadDict = new Dictionary<string, object?>
        {
            ["title"]        = request.Title,
            ["body_html"]    = request.BodyHtml,
            ["product_type"] = request.ProductType,
            ["tags"]         = request.Tags,
            ["vendor"]       = request.Vendor,
            ["status"]       = request.Status,
            ["options"]      = options,
            ["variants"]     = variants,
        };
        if (!string.IsNullOrWhiteSpace(request.ImageUrl))
            payloadDict["images"] = new[] { new { src = request.ImageUrl } };

        ShopifyApiProduct? created = await _client.CreateProductAsync(payloadDict);
        if (created is null) throw new AppException("Shopify no retornó el producto creado.", 502);

        // Set initial inventory for each variant
        for (int i = 0; i < request.Variants.Count && i < created.Variants.Count; i++)
        {
            int qty = request.Variants[i].Qty;
            if (qty > 0)
            {
                await _client.SetInventoryQuantityAsync(
                    created.Variants[i].InventoryItemId, effectiveLocationId, qty);
            }
        }

        var inventoryItemIds = created.Variants.Select(v => v.InventoryItemId).ToList();
        var levels = inventoryItemIds.Count > 0
            ? (await _client.GetInventoryLevelsAsync(inventoryItemIds, effectiveLocationId))
                .ToDictionary(l => l.InventoryItemId, l => l.Available ?? 0)
            : new Dictionary<long, int>();

        return MapDetail(created, levels);
    }

    public async Task<BulkInventoryUpdateResponse> BulkSetInventoryAsync(BulkInventoryUpdateRequest request)
    {
        int updated = 0, failed = 0;
        var errors = new List<string>();

        foreach (BulkInventoryItem item in request.Items)
        {
            try
            {
                bool ok = await _client.SetInventoryQuantityAsync(
                    item.InventoryItemId, request.LocationId, item.Available);
                if (ok) updated++; else failed++;
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"Item {item.InventoryItemId}: {ex.Message}");
            }
        }

        return new BulkInventoryUpdateResponse { Updated = updated, Failed = failed, Errors = errors };
    }

    // ── Collections ──────────────────────────────────────────────────────────

    public async Task<List<ShopifyCollectionResponse>> GetAllCollectionsAsync()
    {
        var custom = (await _client.GetCustomCollectionsAsync())
            .Select(c => new ShopifyCollectionResponse
            {
                Id      = c.Id,
                Title   = c.Title,
                Handle  = c.Handle,
                Type    = "custom",
                ImageUrl = c.Image?.Src,
            });

        var smart = (await _client.GetSmartCollectionsAsync())
            .Select(c => new ShopifyCollectionResponse
            {
                Id      = c.Id,
                Title   = c.Title,
                Handle  = c.Handle,
                Type    = "smart",
                ImageUrl = c.Image?.Src,
            });

        return custom.Concat(smart).OrderBy(c => c.Title).ToList();
    }

    public async Task<List<ShopifyCollectResponse>> GetProductCollectsAsync(long productId)
    {
        List<ShopifyApiCollect> collects = await _client.GetProductCollectsAsync(productId);
        return collects.Select(c => new ShopifyCollectResponse
        {
            Id           = c.Id,
            CollectionId = c.CollectionId,
            ProductId    = c.ProductId,
        }).ToList();
    }

    public async Task UpdateProductCollectionsAsync(
        long productId, List<long> addCollectionIds, List<long> removeCollectIds)
    {
        var removeTasks = removeCollectIds.Select(id => _client.RemoveCollectAsync(id));
        await Task.WhenAll(removeTasks);

        var addTasks = addCollectionIds.Select(colId => _client.AddCollectAsync(productId, colId));
        await Task.WhenAll(addTasks);
    }

    // ── Inventory per location ────────────────────────────────────────────────

    public async Task<List<ShopifyInventoryLevelResponse>> GetProductInventoryAsync(long productId)
    {
        ShopifyApiProduct? product = await _client.GetProductAsync(productId);
        if (product is null) return [];

        List<long> invItemIds = product.Variants.Select(v => v.InventoryItemId).Distinct().ToList();
        if (invItemIds.Count == 0) return [];

        // No location filter → returns levels for all locations
        List<ShopifyApiInventoryLevel> levels = await _client.GetInventoryLevelsAsync(invItemIds, null);

        // Map location IDs to names
        List<ShopifyApiLocation> locations = await _client.GetLocationsAsync();
        var locationMap = locations.ToDictionary(l => l.Id, l => l.Name);

        // Map inventory item ID → variant ID
        var itemToVariant = product.Variants.ToDictionary(v => v.InventoryItemId, v => v.Id);

        return levels.Select(l => new ShopifyInventoryLevelResponse
        {
            InventoryItemId = l.InventoryItemId,
            VariantId       = itemToVariant.GetValueOrDefault(l.InventoryItemId),
            LocationId      = l.LocationId,
            LocationName    = locationMap.GetValueOrDefault(l.LocationId, l.LocationId.ToString()),
            Available       = l.Available ?? 0,
        }).ToList();
    }

    public async Task<object> GetInventoryLevelAsync(long inventoryItemId, long locationId)
    {
        List<ShopifyApiInventoryLevel> levels = await _client.GetInventoryLevelsAsync(
            new[] { inventoryItemId }, locationId);

        int available = levels.FirstOrDefault(l =>
            l.InventoryItemId == inventoryItemId && l.LocationId == locationId)?.Available ?? 0;

        return new { inventory_item_id = inventoryItemId, location_id = locationId, available };
    }

    // ── Inventory transfer ────────────────────────────────────────────────────

    public async Task<ShopifyInventoryTransferResponse> TransferInventoryAsync(
        ShopifyInventoryTransferRequest req, string? performedBy = null)
    {
        if (req.FromLocationId == req.ToLocationId)
            throw new AppException("La sucursal de origen y destino deben ser distintas.", 422);
        if (req.Quantity <= 0)
            throw new AppException("La cantidad debe ser mayor a cero.", 422);

        // Resolve location names for the record
        List<ShopifyApiLocation> locations = await _client.GetLocationsAsync();
        var locationMap = locations.ToDictionary(l => l.Id, l => l.Name);

        string fromName = locationMap.GetValueOrDefault(req.FromLocationId, req.FromLocationId.ToString());
        string toName   = locationMap.GetValueOrDefault(req.ToLocationId,   req.ToLocationId.ToString());

        // Deduct from source — throws AppException with Shopify error details on failure
        await _client.AdjustInventoryQuantityAsync(req.InventoryItemId, req.FromLocationId, -req.Quantity);

        // Add to destination — rollback source deduction if this fails
        try
        {
            await _client.AdjustInventoryQuantityAsync(req.InventoryItemId, req.ToLocationId, +req.Quantity);
        }
        catch (AppException ex)
        {
            await _client.AdjustInventoryQuantityAsync(req.InventoryItemId, req.FromLocationId, +req.Quantity);
            throw new AppException($"No se pudo agregar el inventario en {toName} (se revirtió el descuento). {ex.Message}", 502);
        }

        // Record in database
        var record = new ShopifyTransfer
        {
            ShopifyProductId = req.ShopifyProductId,
            ShopifyVariantId = req.ShopifyVariantId,
            InventoryItemId  = req.InventoryItemId,
            ProductTitle     = req.ProductTitle,
            VariantTitle     = req.VariantTitle,
            FromLocationId   = req.FromLocationId,
            FromLocationName = fromName,
            ToLocationId     = req.ToLocationId,
            ToLocationName   = toName,
            Quantity         = req.Quantity,
            Reason           = req.Reason,
            CreatedBy        = performedBy,
            CreatedAt        = DateTime.UtcNow,
        };
        _db.ShopifyTransfers.Add(record);
        await _db.SaveChangesAsync();

        return MapTransfer(record);
    }

    public async Task<List<ShopifyInventoryTransferResponse>> GetTransferHistoryAsync(int limit = 100)
    {
        return await _db.ShopifyTransfers
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .Select(t => new ShopifyInventoryTransferResponse
            {
                Id               = t.Id,
                ProductTitle     = t.ProductTitle,
                VariantTitle     = t.VariantTitle,
                FromLocationName = t.FromLocationName,
                ToLocationName   = t.ToLocationName,
                Quantity         = t.Quantity,
                Reason           = t.Reason,
                CreatedAt        = t.CreatedAt,
                CreatedBy        = t.CreatedBy,
            })
            .ToListAsync();
    }

    // ── Image upload ──────────────────────────────────────────────────────────

    public async Task<ShopifyImageResponse> UploadProductImageAsync(
        long productId, string base64Data, string filename)
    {
        ShopifyApiImage? img = await _client.AddProductImageAsync(productId, base64Data, filename);
        if (img is null)
            throw new AppException("Shopify no retornó la imagen creada.", 502);

        return new ShopifyImageResponse { Id = img.Id, Src = img.Src, Alt = img.Alt };
    }

    private static ShopifyInventoryTransferResponse MapTransfer(ShopifyTransfer t) => new()
    {
        Id               = t.Id,
        ProductTitle     = t.ProductTitle,
        VariantTitle     = t.VariantTitle,
        FromLocationName = t.FromLocationName,
        ToLocationName   = t.ToLocationName,
        Quantity         = t.Quantity,
        Reason           = t.Reason,
        CreatedAt        = t.CreatedAt,
        CreatedBy        = t.CreatedBy,
    };

    // ── Private helpers ───────────────────────────────────────────────────────

    private static ShopifyProductSummary MapGqlLiteSummary(
        ShopifyGqlProductLite p, Dictionary<long, int>? levelMap = null)
    {
        long id = ParseGidLong(p.Id);
        int totalStock = levelMap is not null
            ? p.Variants.Edges.Sum(e => levelMap.GetValueOrDefault(ParseGidLong(e.Node.InventoryItem?.Id), 0))
            : p.TotalInventory;
        return new ShopifyProductSummary
        {
            Id           = id,
            Title        = p.Title,
            ProductType  = p.ProductType,
            Tags         = p.Tags.Count > 0 ? string.Join(",", p.Tags) : null,
            Vendor       = p.Vendor,
            Status       = p.Status.ToLowerInvariant(),
            ImageUrl     = p.FeaturedImage?.Url,
            VariantCount = p.VariantsCount?.Count ?? 1,
            MinPrice     = ParseDecimal(p.PriceRangeV2?.MinVariantPrice.Amount),
            MaxPrice     = ParseDecimal(p.PriceRangeV2?.MaxVariantPrice.Amount),
            TotalStock   = totalStock,
        };
    }

    private static long ParseGidLong(string? gid)
    {
        if (string.IsNullOrEmpty(gid)) return 0;
        int slash = gid.LastIndexOf('/');
        return slash >= 0 && long.TryParse(gid[(slash + 1)..], out long n) ? n : 0;
    }

    private static string BuildProductGqlQuery(string search, string status)
    {
        string statusFilter = status switch
        {
            "active"   => "status:active",
            "archived" => "status:archived",
            "draft"    => "status:draft",
            _          => "",
        };
        return $"{search.Trim()} {statusFilter}".Trim();
    }

    private static IEnumerable<ShopifyApiProduct> FilterProducts(
        IEnumerable<ShopifyApiProduct> products, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return products;
        string q = search.Trim();
        return products.Where(p =>
            p.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            (p.Tags        ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ||
            (p.ProductType ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ||
            p.Variants.Any(v => (v.Sku ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)));
    }

    private async Task<long> ResolveLocationIdAsync()
    {
        if (_resolvedLocationId.HasValue) return _resolvedLocationId.Value;

        if (_opts.DefaultLocationId > 0)
        {
            _resolvedLocationId = _opts.DefaultLocationId;
            return _resolvedLocationId.Value;
        }

        // Auto-detect first active location
        List<ShopifyApiLocation> locations = await _client.GetLocationsAsync();
        ShopifyApiLocation? active = locations.FirstOrDefault(l => l.Active);
        _resolvedLocationId = active?.Id ?? 0;
        return _resolvedLocationId.Value;
    }

    private static ShopifyProductSummary MapSummary(ShopifyApiProduct p, Dictionary<long, int> levelMap)
    {
        decimal minPrice = p.Variants.Count > 0 ? p.Variants.Min(v => ParseDecimal(v.Price)) : 0m;
        decimal maxPrice = p.Variants.Count > 0 ? p.Variants.Max(v => ParseDecimal(v.Price)) : 0m;
        int totalStock   = p.Variants.Sum(v => levelMap.GetValueOrDefault(v.InventoryItemId, 0));

        return new ShopifyProductSummary
        {
            Id           = p.Id,
            Title        = p.Title,
            ProductType  = p.ProductType,
            Tags         = p.Tags,
            Vendor       = p.Vendor,
            Status       = p.Status,
            ImageUrl     = p.Image?.Src ?? p.Images.FirstOrDefault()?.Src,
            VariantCount = p.Variants.Count,
            MinPrice     = minPrice,
            MaxPrice     = maxPrice,
            TotalStock   = totalStock,
        };
    }

    private static ShopifyProductDetailResponse MapDetail(ShopifyApiProduct p, Dictionary<long, int> levelMap)
        => new()
        {
            Id          = p.Id,
            Title       = p.Title,
            BodyHtml    = p.BodyHtml,
            Status      = p.Status,
            Handle      = p.Handle,
            ProductType = p.ProductType,
            Tags        = p.Tags,
            Vendor      = p.Vendor,
            ImageUrl    = p.Image?.Src ?? p.Images.FirstOrDefault()?.Src,
            Options     = p.Options.Select(o => new ShopifyProductOptionResponse
            {
                Id       = o.Id,
                Name     = o.Name,
                Position = o.Position,
                Values   = o.Values,
            }).ToList(),
            Variants    = p.Variants.Select(v => MapVariant(v,
                levelMap.GetValueOrDefault(v.InventoryItemId, 0))).ToList(),
            Images      = p.Images.Select(i => new ShopifyImageResponse
            {
                Id  = i.Id,
                Src = i.Src,
                Alt = i.Alt,
            }).ToList(),
        };

    private static ShopifyVariantResponse MapVariant(ShopifyApiVariant v, int inventoryQty)
        => new()
        {
            Id                  = v.Id,
            ProductId           = v.ProductId,
            Title               = v.Title,
            Sku                 = v.Sku,
            Price               = ParseDecimal(v.Price),
            CompareAtPrice      = string.IsNullOrWhiteSpace(v.CompareAtPrice) ? null : ParseDecimal(v.CompareAtPrice),
            Option1             = v.Option1,
            Option2             = v.Option2,
            Option3             = v.Option3,
            InventoryItemId     = v.InventoryItemId,
            InventoryQty        = inventoryQty,
            Position            = v.Position,
            InventoryManagement = v.InventoryManagement,
        };

    /// <summary>
    /// Detects which option position (1-3) corresponds to Color and Size
    /// by matching common option names used in Spanish/English Shopify stores.
    /// </summary>
    private static (int colorPos, int sizePos) DetectOptionPositions(ShopifyApiProduct product)
    {
        int colorPos = 0;
        int sizePos  = 0;

        foreach (ShopifyApiProductOption opt in product.Options)
        {
            string n = opt.Name.ToLowerInvariant();
            if (n is "color" or "colour" or "color principal")
                colorPos = opt.Position;
            else if (n is "talla" or "talle" or "size" or "tamaño")
                sizePos = opt.Position;
        }

        return (colorPos, sizePos);
    }

    private static string? GetOption(ShopifyApiVariant v, int position) => position switch
    {
        1 => string.IsNullOrWhiteSpace(v.Option1) ? null : v.Option1,
        2 => string.IsNullOrWhiteSpace(v.Option2) ? null : v.Option2,
        3 => string.IsNullOrWhiteSpace(v.Option3) ? null : v.Option3,
        _ => null,
    };

    private static decimal ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0m;
        return decimal.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out decimal d) ? d : 0m;
    }
}
