using System.Text.Json.Serialization;

namespace AdminHiitop.Api.Application.DTOs.ElectronicBilling;

/// <summary>
/// Nubefact "enviar_correo" operation — resends an already-generated document to a customer email.
/// See Nubefact integration manual, Operation: enviar_correo.
/// </summary>
public sealed class NubeFactSendEmailRequest
{
    [JsonPropertyName("operacion")]
    public string Operacion { get; set; } = "enviar_correo";

    [JsonPropertyName("tipo_de_comprobante")]
    public int TipoDeComprobante { get; set; }

    [JsonPropertyName("serie")]
    public string Serie { get; set; } = string.Empty;

    [JsonPropertyName("numero")]
    public int Numero { get; set; }

    [JsonPropertyName("correo_electronico")]
    public string CorreoElectronico { get; set; } = string.Empty;
}
