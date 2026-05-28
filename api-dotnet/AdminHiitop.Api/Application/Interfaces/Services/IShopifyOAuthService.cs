namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IShopifyOAuthService
{
    /// <summary>Generates the Shopify authorization URL and caches the state nonce.</summary>
    string BuildInstallUrl(string shop, out string state);

    /// <summary>Exchanges the authorization code for a permanent offline token and persists it.</summary>
    Task<bool> HandleCallbackAsync(string shop, string code, string state, string hmacFromShopify, IQueryCollection rawQuery);

    /// <summary>Returns true if a valid token exists for the configured shop domain.</summary>
    Task<bool> IsConnectedAsync();

    /// <summary>Removes the stored token for the configured shop domain.</summary>
    Task DisconnectAsync();
}
