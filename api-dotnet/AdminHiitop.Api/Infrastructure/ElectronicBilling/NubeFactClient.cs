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

    public Task<NubeFactSubmitResult> SendDocumentAsync(NubeFactDocumentRequest request)
        => SendAsync(request, request);

    public Task<NubeFactSubmitResult> SendGuideDocumentAsync(NubeFactGuideDocumentRequest request)
        => SendAsync(request, new NubeFactDocumentRequest());

    public Task<NubeFactSubmitResult> ConsultGuideAsync(NubeFactConsultGuideRequest request)
        => SendAsync(request, new NubeFactDocumentRequest());

    /// <summary>
    /// Calls Nubefact "enviar_correo" operation to resend an existing document to a customer email.
    /// </summary>
    public Task<NubeFactSubmitResult> SendDocumentByEmailAsync(NubeFactSendEmailRequest request)
        => SendAsync(request, new NubeFactDocumentRequest());

    /// <summary>
    /// Calls Nubefact "comunicar_baja" to void a document via SUNAT low-communication.
    /// Only valid within 7 days of emission.
    /// </summary>
    public Task<NubeFactSubmitResult> SendBajaAsync(NubeFactBajaRequest request)
        => SendAsync(request, new NubeFactDocumentRequest());

    private async Task<NubeFactSubmitResult> SendAsync(object payload, NubeFactDocumentRequest requestRef)
    {
        string apiUrl = ResolveApiUrl();

        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            throw new AppException("La URL de Nubefact no está configurada.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            throw new AppException("El token de Nubefact no está configurado.");
        }

        string requestJson = JsonSerializer.Serialize(payload, JsonOptions);

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
                Errors = "La respuesta de Nubefact no tiene un JSON válido."
            };
        }

        return new NubeFactSubmitResult
        {
            Success = httpResponse.IsSuccessStatusCode && string.IsNullOrWhiteSpace(response.Errors),
            ProviderName = "NubeFact",
            Environment = ResolveEnvironmentName(),
            Endpoint = apiUrl,
            Request = requestRef,
            Response = response,
            RawRequestJson = requestJson,
            RawResponseJson = responseJson
        };
    }

    private string ResolveApiUrl()
        => !string.IsNullOrWhiteSpace(_options.ProductionApiUrl)
            ? _options.ProductionApiUrl.Trim()
            : _options.ApiUrl.Trim();

    private string ResolveEnvironmentName() => "Production";
}
