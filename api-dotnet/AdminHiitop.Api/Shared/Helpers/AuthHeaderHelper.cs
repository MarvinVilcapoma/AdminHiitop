namespace AdminHiitop.Api.Shared.Helpers;

public static class AuthHeaderHelper
{
    public static string? ReadBearerToken(HttpRequest request)
    {
        string? authorization = request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return authorization["Bearer ".Length..].Trim();
    }
}
