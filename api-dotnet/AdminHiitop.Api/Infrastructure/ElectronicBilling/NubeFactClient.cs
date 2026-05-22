using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AdminHiitop.Api.Application.DTOs.ElectronicBilling;
using AdminHiitop.Api.Shared.Exceptions;
using Microsoft.Extensions.Options;

namespace AdminHiitop.Api.Infrastructure.ElectronicBilling;

public sealed class NubeFactClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly NubeFactOptions _options;

    public NubeFactClient(HttpClient httpClient, IOptions<NubeFactOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public Task<bool> ValidateConfigurationAsync()
    {
        bool isValid = !string.IsNullOrWhiteSpace(ResolveApiUrl())
            && !string.IsNullOrWhiteSpace(_options.ApiToken);

        return Task.FromResult(isValid);
    }

    public async Task<NubeFactSubmitResult> SendDocumentAsync(NubeFactDocumentRequest request)
    {
        string apiUrl = ResolveApiUrl();

        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            throw new AppException("La URL de Nubefact no est� configurada.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            throw new AppException("El token de Nubefact no est� configurado.");
        }

        string requestJson = JsonSerializer.Serialize(request, JsonOptions);

        using HttpRequestMessage httpRequest = new(HttpMethod.Post, apiUrl);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Token", $"token={_options.ApiToken}");
        httpRequest.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        using CancellationTokenSource timeoutSource = new(TimeSpan.FromSeconds(Math.Max(_options.TimeoutSeconds, 5)));

        HttpResponseMessage httpResponse = await _httpClient.SendAsync(httpRequest, timeoutSource.Token);
        string responseJson = await httpResponse.Content.ReadAsStringAsync(timeoutSource.Token);

        NubeFactDocumentResponse response;

        try
        {
            response = JsonSerializer.Deserialize<NubeFactDocumentResponse>(responseJson, JsonOptions) ?? new NubeFactDocumentResponse
            {
                Errors = "No se pudo interpretar la respuesta de Nubefact."
            };
        }
        catch (JsonException)
        {
            response = new NubeFactDocumentResponse
            {
                Errors = "La respuesta de Nubefact no tiene un JSON v�lido."
            };
        }

        return new NubeFactSubmitResult
        {
            Success = httpResponse.IsSuccessStatusCode && string.IsNullOrWhiteSpace(response.Errors),
            ProviderName = "NubeFact",
            Environment = ResolveEnvironmentName(),
            Endpoint = apiUrl,
            Request = request,
            Response = response,
            RawRequestJson = requestJson,
            RawResponseJson = responseJson
        };
    }

    private string ResolveApiUrl()
    {
        // Demo environment always uses DemoApiUrl
        if (string.Equals(_options.Environment, "Demo", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(_options.DemoApiUrl))
        {
            return _options.DemoApiUrl.Trim();
        }

        // Production: prefer explicit ProductionApiUrl, fallback to ApiUrl
        if (!string.IsNullOrWhiteSpace(_options.ProductionApiUrl))
        {
            return _options.ProductionApiUrl.Trim();
        }

        return _options.ApiUrl.Trim();
    }

    private string ResolveEnvironmentName()
    {
        return string.Equals(_options.Environment, "Production", StringComparison.OrdinalIgnoreCase)
            ? "Production"
            : "Demo";
    }
}
