using System.Text.Json.Serialization;

namespace AdminHiitop.Api.Application.DTOs.ElectronicBilling;

public sealed class NubeFactGuideItemRequest
{
    [JsonPropertyName("unidad_de_medida")]
    public string UnidadDeMedida { get; set; } = "NIU";

    [JsonPropertyName("codigo")]
    public string Codigo { get; set; } = string.Empty;

    [JsonPropertyName("descripcion")]
    public string Descripcion { get; set; } = string.Empty;

    [JsonPropertyName("cantidad")]
    public decimal Cantidad { get; set; }
}
