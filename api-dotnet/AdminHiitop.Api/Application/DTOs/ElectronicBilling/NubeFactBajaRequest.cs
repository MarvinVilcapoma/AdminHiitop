using System.Text.Json.Serialization;

namespace AdminHiitop.Api.Application.DTOs.ElectronicBilling;

/// <summary>
/// Nubefact "comunicar_baja" — voids a document sent to SUNAT within the same day.
/// Only valid for documents issued the same calendar day (Peru time).
/// SUNAT allows voiding invoices within 7 days of emission via low-communication.
/// </summary>
public sealed class NubeFactBajaRequest
{
    [JsonPropertyName("operacion")]
    public string Operacion { get; set; } = "comunicar_baja";

    [JsonPropertyName("tipo_de_comprobante")]
    public int TipoDeComprobante { get; set; }  // 1=Factura, 2=Boleta

    [JsonPropertyName("serie")]
    public string Serie { get; set; } = string.Empty;

    [JsonPropertyName("correlativo")]
    public int Correlativo { get; set; }

    [JsonPropertyName("fecha_de_emision")]
    public string FechaDeEmision { get; set; } = string.Empty;  // "dd/mm/yyyy"

    [JsonPropertyName("motivo")]
    public string Motivo { get; set; } = string.Empty;
}
