using System.Text.Json.Serialization;

namespace AdminHiitop.Api.Application.DTOs.ElectronicBilling;

public sealed class NubeFactGuideRequest
{
    [JsonPropertyName("guia_tipo")]
    public int GuiaTipo { get; set; }

    [JsonPropertyName("guia_serie_numero")]
    public string GuiaSerieNumero { get; set; } = string.Empty;
}
