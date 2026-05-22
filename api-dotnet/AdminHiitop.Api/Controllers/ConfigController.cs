using AdminHiitop.Api.Infrastructure.Shopify;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public ConfigController(IOptions<ShopifyOptions> shopify)
    {
        _shopify = shopify.Value;
    }

    /// <summary>
    /// Returns the current feature flags that the Angular app needs on startup.
    /// Consumed by AppConfigService via APP_INITIALIZER.
    /// </summary>
    [HttpGet("app")]
    public IActionResult GetAppConfig() => Ok(new
    {
        shopify_mode       = _shopify.UseShopifyMode,
        use_shopify_stock  = _shopify.UseShopifyStock,
        sync_inventory     = _shopify.SyncInventory,
        shop_domain        = _shopify.ShopDomain,
        store_name         = ExtractStoreName(_shopify.ShopDomain),
        shopify_configured = !string.IsNullOrWhiteSpace(_shopify.ShopDomain)
                             && (!string.IsNullOrWhiteSpace(_shopify.AccessToken)
                                 || !string.IsNullOrWhiteSpace(_shopify.ClientId)),
    });

    private static string ExtractStoreName(string domain)
    {
        // "hiitop-3136.myshopify.com" → "hiitop-3136"
        if (string.IsNullOrWhiteSpace(domain)) return "";
        int dot = domain.IndexOf('.');
        return dot > 0 ? domain[..dot] : domain;
    }
}
