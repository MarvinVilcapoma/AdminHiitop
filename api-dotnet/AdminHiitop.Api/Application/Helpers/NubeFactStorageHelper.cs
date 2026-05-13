using System.Text.Json;
using AdminHiitop.Api.Application.DTOs.ElectronicBilling;

namespace AdminHiitop.Api.Application.Helpers;

public static class NubeFactStorageHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null
    };

    public static NubeFactDocumentResponse? ReadStoredResponse(string? rawResponseJson)
    {
        if (string.IsNullOrWhiteSpace(rawResponseJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<NubeFactDocumentResponse>(rawResponseJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static byte[]? DecodeBase64(string? base64Content)
    {
        if (string.IsNullOrWhiteSpace(base64Content))
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(base64Content);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
