using System.Text.Json.Serialization;

namespace AdminHiitop.Api.Application.DTOs.ElectronicBilling;

public sealed class NubeFactConsultGuideRequest
{
    [JsonPropertyName("operacion")]
    public string Operacion { get; set; } = "consultar_guia";

    [JsonPropertyName("tipo_de_comprobante")]
    public int TipoDeComprobante { get; set; }

    [JsonPropertyName("serie")]
    public string Serie { get; set; } = string.Empty;

    [JsonPropertyName("numero")]
    public string Numero { get; set; } = string.Empty;
}
