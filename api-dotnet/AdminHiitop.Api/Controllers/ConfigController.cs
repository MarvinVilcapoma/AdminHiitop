using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Infrastructure.Shopify;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AdminHiitop.Api.Controllers;

/// <summary>
/// Public endpoint — exposes feature flags to the frontend.
/// No authentication required so it can be read before login.
/// </summary>
[ApiController]
[Route("api/config")]
[AllowAnonymous]
public sealed class ConfigController : ControllerBase
{
    private readonly ShopifyOptions _shopify;
    private readonly AdminHiitopDbContext _db;

    public ConfigController(IOptions<ShopifyOptions> shopify, AdminHiitopDbContext db)
    {
        _shopify = shopify.Value;
        _db = db;
    }

    /// <summary>
    /// Returns the current feature flags that the Angular app needs on startup.
    /// Consumed by AppConfigService via APP_INITIALIZER.
    /// </summary>
    [HttpGet("app")]
    public async Task<IActionResult> GetAppConfig()
    {
        var activeModulesSetting = await _db.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == "active_modules");

        // Default: all known module permissions are active
        var defaultModules = new[]
        {
            "dashboard.view", "pos.view", "orders.view", "guides.view",
            "stocks.view", "customers.view", "invoices.view", "finance.view",
            "users.view", "config.order-statuses",
            "products.view", "promotions.view", "sales.view",
        };

        string[]? activeModules = null;
        if (!string.IsNullOrWhiteSpace(activeModulesSetting?.Value))
        {
            activeModules = activeModulesSetting.Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return Ok(new
        {
            shopify_mode                = _shopify.UseShopifyMode,
            use_shopify_stock           = _shopify.UseShopifyStock,
            sync_inventory              = _shopify.SyncInventory,
            show_stock_source_selector  = _shopify.ShowStockSourceSelector,
            shop_domain                 = _shopify.ShopDomain,
            store_name                  = ExtractStoreName(_shopify.ShopDomain),
            shopify_configured          = !string.IsNullOrWhiteSpace(_shopify.ShopDomain)
                                          && (!string.IsNullOrWhiteSpace(_shopify.AccessToken)
                                              || !string.IsNullOrWhiteSpace(_shopify.ClientId)),
            active_modules              = activeModules ?? defaultModules,
        });
    }

    private static string ExtractStoreName(string domain)
    {
        // "hiitop-3136.myshopify.com" → "hiitop-3136"
        if (string.IsNullOrWhiteSpace(domain)) return "";
        int dot = domain.IndexOf('.');
        return dot > 0 ? domain[..dot] : domain;
    }
}
