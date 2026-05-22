using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;

namespace AdminHiitop.Api.Infrastructure.Auth;

public sealed class SessionTokenStore
{
    private readonly ConcurrentDictionary<string, SessionTokenEntry> _tokens = new();
    private readonly int _expirationDays;

    public SessionTokenStore(IConfiguration configuration)
    {
        _expirationDays = configuration.GetValue<int>("Session:TokenExpirationDays", 1);
    }

    public string Create(int userId)
    {
        string token = Guid.NewGuid().ToString("N");
        _tokens[token] = new SessionTokenEntry
        {
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(_expirationDays)
        };

        return token;
    }

    public int? GetUserId(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        if (!_tokens.TryGetValue(token, out SessionTokenEntry? entry))
        {
            return null;
        }

        if (entry.ExpiresAt <= DateTime.UtcNow)
        {
            _tokens.TryRemove(token, out _);
            return null;
        }

        return entry.UserId;
    }

    public void Remove(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        _tokens.TryRemove(token, out _);
    }

    private sealed class SessionTokenEntry
    {
        public int UserId { get; init; }
        public DateTime ExpiresAt { get; init; }
    }
}
