using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AdminHiitop.Api.Infrastructure.Shopify;

public static class ShopifyOAuthHelper
{
    private static readonly Regex ShopDomainRegex =
        new(@"^[a-zA-Z0-9][a-zA-Z0-9\-]*\.myshopify\.com$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    public static bool IsValidShopDomain(string? shop)
        => !string.IsNullOrWhiteSpace(shop) && ShopDomainRegex.IsMatch(shop.Trim());

    public static string GenerateState()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    /// <summary>
    /// Validates the HMAC sent by Shopify on the OAuth callback.
    /// Shopify computes: HMAC-SHA256(all query params except hmac, sorted and joined with &amp;)
    /// </summary>
    public static bool IsValidHmac(IQueryCollection query, string clientSecret)
    {
        string? hmacParam = query["hmac"];
        if (string.IsNullOrWhiteSpace(hmacParam)) return false;

        // Build message: all params except 'hmac', sorted alphabetically, percent-encode & and %
        var pairs = query
            .Where(kv => kv.Key != "hmac")
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv =>
            {
                string k = kv.Key.Replace("%", "%25").Replace("&", "%26");
                string v = (kv.Value.ToString() ?? "").Replace("%", "%25").Replace("&", "%26");
                return $"{k}={v}";
            });

        string message = string.Join("&", pairs);
        byte[] key  = Encoding.UTF8.GetBytes(clientSecret);
        byte[] data = Encoding.UTF8.GetBytes(message);
        byte[] hash = HMACSHA256.HashData(key, data);
        string computed = Convert.ToHexString(hash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(hmacParam.Trim().ToLowerInvariant()));
    }
}
