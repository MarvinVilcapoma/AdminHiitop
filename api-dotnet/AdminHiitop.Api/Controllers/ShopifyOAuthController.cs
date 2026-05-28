using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Infrastructure.Auth;
using AdminHiitop.Api.Infrastructure.Shopify;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AdminHiitop.Api.Controllers;

[ApiController]
[Route("api/shopify/oauth")]
public sealed class ShopifyOAuthController : ControllerBase
{
    private readonly IShopifyOAuthService _oauth;
    private readonly ShopifyOptions       _opts;
    private readonly SessionTokenStore    _sessions;

    public ShopifyOAuthController(
        IShopifyOAuthService oauth,
        IOptions<ShopifyOptions> opts,
        SessionTokenStore sessions)
    {
        _oauth    = oauth;
        _opts     = opts.Value;
        _sessions = sessions;
    }

    private bool IsAuthenticated()
        => _sessions.GetUserId(AuthHeaderHelper.ReadBearerToken(Request)) is not null;

    /// <summary>
    /// Step 1 — Initiates the OAuth install flow.
    /// Redirect the merchant's browser here: GET /api/shopify/oauth/install?shop=hiitop-3136.myshopify.com
    /// The response is a 302 redirect to Shopify's authorization page.
    /// </summary>
    [HttpGet("install")]
    public IActionResult Install([FromQuery] string? shop)
    {
        try
        {
            string target = shop?.Trim() ?? _opts.ShopDomain.Trim();
            string installUrl = _oauth.BuildInstallUrl(target, out _);
            return Redirect(installUrl);
        }
        catch (AppException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Step 2 — Shopify redirects here after the merchant approves access.
    /// Validates HMAC + state, exchanges the code for a permanent token, then
    /// redirects the browser to FrontendRedirectUrl (or returns 200 if not configured).
    /// </summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? shop,
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? hmac)
    {
        if (string.IsNullOrWhiteSpace(shop)  ||
            string.IsNullOrWhiteSpace(code)  ||
            string.IsNullOrWhiteSpace(state) ||
            string.IsNullOrWhiteSpace(hmac))
        {
            return BadRequest(new { message = "Parámetros incompletos en el callback de Shopify." });
        }

        try
        {
            await _oauth.HandleCallbackAsync(shop, code, state, hmac, Request.Query);

            string redirect = _opts.FrontendRedirectUrl.Trim();
            if (!string.IsNullOrWhiteSpace(redirect))
                return Redirect(redirect);

            return Ok(new { message = "Shopify conectado exitosamente.", shop });
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    /// <summary>Returns the current OAuth connection status for the configured shop.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        if (!IsAuthenticated())
            return Unauthorized(new { message = "Token de sesión requerido." });

        bool connected = await _oauth.IsConnectedAsync();
        return Ok(new
        {
            connected,
            shop    = _opts.ShopDomain,
            message = connected ? "Tienda Shopify conectada." : "No hay token OAuth almacenado.",
        });
    }

    /// <summary>Removes the stored OAuth token (disconnects the shop).</summary>
    [HttpPost("disconnect")]
    public async Task<IActionResult> Disconnect()
    {
        if (!IsAuthenticated())
            return Unauthorized(new { message = "Token de sesión requerido." });

        await _oauth.DisconnectAsync();
        return Ok(new { message = "Shopify desconectado." });
    }
}
