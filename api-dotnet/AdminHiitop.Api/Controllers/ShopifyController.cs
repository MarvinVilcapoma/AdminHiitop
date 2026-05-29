using AdminHiitop.Api.Application.DTOs.Shopify;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Infrastructure.Shopify;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AdminHiitop.Api.Controllers;

[ApiController]
[Route("api/shopify")]
public sealed class ShopifyController : ControllerBase
{
    private readonly IShopifyOrderService   _orders;
    private readonly IShopifyProductService _products;

    public ShopifyController(IShopifyOrderService orders, IShopifyProductService products)
    {
        _orders   = orders;
        _products = products;
    }

    // ── Status ────────────────────────────────────────────────────────────────

    /// <summary>Verifica que las credenciales Shopify funcionan y devuelve diagnóstico.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        try
        {
            bool ok = await _orders.TestConnectionAsync();
            return Ok(new
            {
                success  = ok,
                provider = "Shopify",
                message  = ok ? "Conexión exitosa." : "No se pudo conectar a Shopify.",
            });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                success  = false,
                provider = "Shopify",
                message  = ex.Message,
                hint     = "Verifica que el AccessToken en appsettings.json sea correcto (shpat_...) y que el ShopDomain sea válido.",
            });
        }
    }

    // ── Orders ────────────────────────────────────────────────────────────────

    /// <summary>Lista pedidos de Shopify con paginación cursor.</summary>
    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders(
        [FromQuery] int     limit              = 50,
        [FromQuery] string? page_info          = null,
        [FromQuery] string? financial_status   = null,
        [FromQuery] string? fulfillment_status = null,
        [FromQuery] string? created_at_min     = null,
        [FromQuery] string? created_at_max     = null,
        [FromQuery] string? search             = null)
        => Ok(await _orders.GetOrdersAsync(limit, page_info, financial_status, fulfillment_status, created_at_min, created_at_max, search));

    /// <summary>Detalle de un pedido Shopify.</summary>
    [HttpGet("orders/{id:long}")]
    public async Task<IActionResult> GetOrder(long id)
    {
        var order = await _orders.GetOrderAsync(id);
        return order is null ? NotFound(new { message = "Orden no encontrada en Shopify." }) : Ok(order);
    }

    /// <summary>Marca la orden como enviada/fulfillment en Shopify.</summary>
    [HttpPost("orders/{id:long}/fulfill")]
    public async Task<IActionResult> Fulfill(
        long id,
        [FromBody] ShopifyFulfillRequest? body = null)
        => Ok(await _orders.FulfillOrderAsync(id, body?.TrackingNumber, body?.TrackingCompany));

    /// <summary>Cancela la orden en Shopify.</summary>
    [HttpPost("orders/{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id)
        => Ok(await _orders.CancelOrderAsync(id));

    // ── Products — lookup (mismo shape que /api/stocks/lookup) ───────────────

    /// <summary>
    /// Busca variantes de Shopify con stock real. Mismo shape que GET /api/stocks/lookup.
    /// Usar cuando UseShopifyStock = true para alimentar el POS y formulario de pedidos.
    /// </summary>
    [HttpGet("products/lookup")]
    public async Task<IActionResult> ProductsLookup(
        [FromQuery] string? search      = null,
        [FromQuery] int     limit       = 50,
        [FromQuery] long?   location_id = null)
        => Ok(await _products.GetLookupAsync(search, Math.Clamp(limit, 1, 200), location_id));

    // ── Products — CRUD ───────────────────────────────────────────────────────

    /// <summary>Lista productos de Shopify con paginación.</summary>
    [HttpGet("products")]
    public async Task<IActionResult> GetProducts(
        [FromQuery] string? search      = null,
        [FromQuery] int     page        = 1,
        [FromQuery] int     per_page    = 20,
        [FromQuery] string  status      = "active",
        [FromQuery] long?   location_id = null)
        => Ok(await _products.GetProductsAsync(search, Math.Max(1, page), Math.Clamp(per_page, 1, 100), status, location_id));

    /// <summary>Detalle completo de un producto con todas sus variantes e inventario.</summary>
    [HttpGet("products/{id:long}")]
    public async Task<IActionResult> GetProduct(long id, [FromQuery] long? location_id = null)
    {
        var product = await _products.GetProductAsync(id, location_id);
        return product is null
            ? NotFound(new { message = "Producto no encontrado en Shopify." })
            : Ok(product);
    }

    /// <summary>Actualiza metadata del producto (título, descripción, tags, estado).</summary>
    [HttpPut("products/{id:long}")]
    public async Task<IActionResult> UpdateProduct(long id, [FromBody] ShopifyProductUpdateRequest request)
        => Ok(await _products.UpdateProductAsync(id, request));

    /// <summary>Actualiza una variante (precio, SKU, opciones).</summary>
    [HttpPut("variants/{id:long}")]
    public async Task<IActionResult> UpdateVariant(long id, [FromBody] ShopifyVariantUpdateRequest request)
        => Ok(await _products.UpdateVariantAsync(id, request));

    // ── Locations ─────────────────────────────────────────────────────────────

    /// <summary>Lista las ubicaciones/almacenes configurados en Shopify.</summary>
    [HttpGet("locations")]
    public async Task<IActionResult> GetLocations()
        => Ok(await _products.GetLocationsAsync());

    // ── Product creation ──────────────────────────────────────────────────────

    /// <summary>Crea un nuevo producto en Shopify con variantes e inventario inicial.</summary>
    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct(
        [FromBody] ShopifyProductCreateRequest request,
        [FromQuery(Name = "location_id")] long? locationId = null)
        => Ok(await _products.CreateProductAsync(request, locationId));

    // ── Bulk inventory ────────────────────────────────────────────────────────

    /// <summary>Actualiza el inventario de múltiples variantes en una sola operación.</summary>
    [HttpPost("inventory/bulk-set")]
    public async Task<IActionResult> BulkSetInventory([FromBody] BulkInventoryUpdateRequest request)
        => Ok(await _products.BulkSetInventoryAsync(request));

    // ── Inventory adjustment ──────────────────────────────────────────────────

    /// <summary>
    /// Ajusta el inventario Shopify por delta (negativo = descuento, positivo = entrada).
    /// Útil para registrar manualmente un consumo cuando se crea un pedido con stock de Shopify.
    /// </summary>
    [HttpPost("inventory/adjust")]
    public async Task<IActionResult> AdjustInventory([FromBody] ShopifyInventoryAdjustRequest request)
        => Ok(new
        {
            success = await _products.AdjustInventoryAsync(
                request.InventoryItemId, request.LocationId, request.Delta),
        });

    // ── Collections ──────────────────────────────────────────────────────────

    /// <summary>Lista todas las colecciones Shopify (custom + smart).</summary>
    [HttpGet("collections")]
    public async Task<IActionResult> GetCollections()
        => Ok(await _products.GetAllCollectionsAsync());

    /// <summary>Retorna los collects (membresías de colección) de un producto.</summary>
    [HttpGet("products/{id:long}/collects")]
    public async Task<IActionResult> GetProductCollects(long id)
        => Ok(await _products.GetProductCollectsAsync(id));

    /// <summary>Agrega/quita el producto de colecciones.</summary>
    [HttpPut("products/{id:long}/collections")]
    public async Task<IActionResult> UpdateProductCollections(
        long id, [FromBody] ShopifyProductCollectionsUpdateRequest request)
    {
        await _products.UpdateProductCollectionsAsync(id, request.AddCollectionIds, request.RemoveCollectIds);
        return Ok(new { success = true });
    }

    // ── Inventory per location ────────────────────────────────────────────────

    /// <summary>Retorna el inventario de todas las variantes de un producto en todas las sucursales.</summary>
    [HttpGet("products/{id:long}/inventory")]
    public async Task<IActionResult> GetProductInventory(long id)
        => Ok(await _products.GetProductInventoryAsync(id));

    /// <summary>Retorna el stock disponible de un inventory_item en una ubicación específica.</summary>
    [HttpGet("inventory/level")]
    public async Task<IActionResult> GetInventoryLevel(
        [FromQuery(Name = "inventory_item_id")] long inventoryItemId,
        [FromQuery(Name = "location_id")]       long locationId)
        => Ok(await _products.GetInventoryLevelAsync(inventoryItemId, locationId));

    // ── Orders history (multi-page) ───────────────────────────────────────────

    /// <summary>
    /// Obtiene hasta max_orders pedidos de Shopify paginando automáticamente.
    /// Usar para cargar historial completo (hasta 2000 pedidos).
    /// </summary>
    [HttpGet("orders/history")]
    public async Task<IActionResult> GetOrdersHistory(
        [FromQuery] int     max_orders         = 2000,
        [FromQuery] string? financial_status   = null,
        [FromQuery] string? fulfillment_status = null,
        [FromQuery] string? created_at_min     = null,
        [FromQuery] string? created_at_max     = null,
        [FromQuery] string? search             = null)
        => Ok(await _orders.GetAllOrdersHistoryAsync(
            max_orders <= 0 ? 0 : Math.Min(max_orders, 100_000),   // 0 = unlimited
            financial_status, fulfillment_status, created_at_min, created_at_max, search));

    // ── Customers ────────────────────────────────────────────────────────────

    /// <summary>Lista clientes de Shopify con búsqueda y paginación cursor.</summary>
    [HttpGet("customers")]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] string? search    = null,
        [FromQuery] int     limit     = 50,
        [FromQuery] string? page_info = null)
        => Ok(await _orders.GetCustomersAsync(search, Math.Clamp(limit, 1, 250), page_info));

    // ── Inventory transfer ────────────────────────────────────────────────────

    /// <summary>
    /// Transfiere inventario entre dos sucursales Shopify y registra el movimiento en la BD.
    /// </summary>
    [HttpPost("inventory/transfer")]
    public async Task<IActionResult> TransferInventory([FromBody] ShopifyInventoryTransferRequest request)
    {
        string? user = User.Identity?.Name ?? User.FindFirst("email")?.Value;
        var result = await _products.TransferInventoryAsync(request, user);
        return Ok(result);
    }

    /// <summary>Historial de traspasos de inventario entre sucursales.</summary>
    [HttpGet("inventory/transfers")]
    public async Task<IActionResult> GetTransferHistory(
        [FromQuery] int limit = 100)
        => Ok(await _products.GetTransferHistoryAsync(Math.Clamp(limit, 1, 500)));

    // ── Image upload ──────────────────────────────────────────────────────────

    /// <summary>Sube una imagen a un producto Shopify via base64.</summary>
    [HttpPost("products/{id:long}/images/upload")]
    public async Task<IActionResult> UploadProductImage(
        long id, [FromBody] ShopifyImageUploadRequest request)
        => Ok(await _products.UploadProductImageAsync(id, request.Attachment, request.Filename));

    // ── Metrics ───────────────────────────────────────────────────────────────

    /// <summary>Métricas de ventas calculadas desde órdenes de Shopify.</summary>
    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics(
        [FromQuery] DateTime? start_date = null,
        [FromQuery] DateTime? end_date   = null)
        => Ok(await _products.GetMetricsAsync(start_date, end_date));
}

public sealed class ShopifyFulfillRequest
{
    public string? TrackingNumber  { get; set; }
    public string? TrackingCompany { get; set; }
}

public sealed class ShopifyInventoryAdjustRequest
{
    public long InventoryItemId { get; set; }
    public long LocationId      { get; set; }
    public int  Delta           { get; set; }  // negative = deduct
}
