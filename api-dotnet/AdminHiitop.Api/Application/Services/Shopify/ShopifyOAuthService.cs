using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Shopify.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Infrastructure.Shopify;
using AdminHiitop.Api.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AdminHiitop.Api.Application.Services.Shopify;

public sealed class ShopifyOAuthService : IShopifyOAuthService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ShopifyOptions      _opts;
    private readonly AdminHiitopDbContext _db;
    private readonly IMemoryCache        _cache;
    private readonly HttpClient          _http;

    private const string StateCachePrefix = "shopify_oauth_state_";

    public ShopifyOAuthService(
        IOptions<ShopifyOptions> opts,
        AdminHiitopDbContext db,
        IMemoryCache cache,
        HttpClient http)
    {
        _opts  = opts.Value;
        _db    = db;
        _cache = cache;
        _http  = http;
    }

    public string BuildInstallUrl(string shop, out string state)
    {
        if (!ShopifyOAuthHelper.IsValidShopDomain(shop))
            throw new AppException($"Dominio de tienda inválido: '{shop}'.", 400);

        if (string.IsNullOrWhiteSpace(_opts.ClientId))
            throw new AppException("Shopify ClientId no está configurado en appsettings.json.", 500);

        if (string.IsNullOrWhiteSpace(_opts.PublicApiBaseUrl))
            throw new AppException("Shopify:PublicApiBaseUrl no está configurado en appsettings.json.", 500);

        state = ShopifyOAuthHelper.GenerateState();
        _cache.Set(StateCachePrefix + state, shop, TimeSpan.FromMinutes(10));

        string redirectUri = Uri.EscapeDataString(
            $"{_opts.PublicApiBaseUrl.TrimEnd('/')}/api/shopify/oauth/callback");

        string scopes = Uri.EscapeDataString(_opts.Scopes.Trim());

        // No grant_options[]=per-user → Shopify issues an offline (permanent) token
        return $"https://{shop}/admin/oauth/authorize"
             + $"?client_id={_opts.ClientId}"
             + $"&scope={scopes}"
             + $"&redirect_uri={redirectUri}"
             + $"&state={state}";
    }

    public async Task<bool> HandleCallbackAsync(
        string shop, string code, string state,
        string hmacFromShopify, IQueryCollection rawQuery)
    {
        // 1. Validate HMAC
        if (!ShopifyOAuthHelper.IsValidHmac(rawQuery, _opts.ClientSecret))
            throw new AppException("HMAC inválido — posible solicitud falsa.", 400);

        // 2. Validate state nonce
        if (!_cache.TryGetValue(StateCachePrefix + state, out string? cachedShop)
            || cachedShop != shop)
            throw new AppException("State inválido o expirado.", 400);

        _cache.Remove(StateCachePrefix + state);

        // 3. Validate shop domain
        if (!ShopifyOAuthHelper.IsValidShopDomain(shop))
            throw new AppException($"Dominio de tienda inválido: '{shop}'.", 400);

        // 4. Exchange code for offline access token
        string tokenUrl = $"https://{shop}/admin/oauth/access_token";
        var body = new
        {
            client_id     = _opts.ClientId,
            client_secret = _opts.ClientSecret,
            code,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body, JsonOpts),
                Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using HttpResponseMessage response = await _http.SendAsync(request);
        string respBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new AppException($"Shopify rechazó el intercambio de código: {respBody}", 502);

        using JsonDocument doc = JsonDocument.Parse(respBody);
        if (!doc.RootElement.TryGetProperty("access_token", out JsonElement tokenEl))
            throw new AppException("Shopify no devolvió access_token.", 502);

        string accessToken = tokenEl.GetString()
            ?? throw new AppException("Shopify devolvió access_token vacío.", 502);

        string scope = doc.RootElement.TryGetProperty("scope", out JsonElement scopeEl)
            ? scopeEl.GetString() ?? ""
            : "";

        // 5. Persist token (upsert by shop domain)
        DateTime now = DateTime.UtcNow;
        ShopifyStoreConnection? existing = await _db.ShopifyStoreConnections
            .FirstOrDefaultAsync(c => c.ShopDomain == shop);

        if (existing is null)
        {
            _db.ShopifyStoreConnections.Add(new ShopifyStoreConnection
            {
                ShopDomain  = shop,
                AccessToken = accessToken,
                Scope       = scope,
                InstalledAt = now,
                UpdatedAt   = now,
            });
        }
        else
        {
            existing.AccessToken = accessToken;
            existing.Scope       = scope;
            existing.UpdatedAt   = now;
        }

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IsConnectedAsync()
    {
        string shop = _opts.ShopDomain.Trim();
        if (string.IsNullOrWhiteSpace(shop)) return false;

        return await _db.ShopifyStoreConnections
            .AnyAsync(c => c.ShopDomain == shop && c.AccessToken != "");
    }

    public async Task DisconnectAsync()
    {
        string shop = _opts.ShopDomain.Trim();
        ShopifyStoreConnection? conn = await _db.ShopifyStoreConnections
            .FirstOrDefaultAsync(c => c.ShopDomain == shop);

        if (conn is not null)
        {
            _db.ShopifyStoreConnections.Remove(conn);
            await _db.SaveChangesAsync();
        }
    }
}
