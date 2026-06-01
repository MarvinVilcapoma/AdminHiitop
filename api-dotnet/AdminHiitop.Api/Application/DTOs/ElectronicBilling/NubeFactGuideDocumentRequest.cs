using System.Text.Json.Serialization;

namespace AdminHiitop.Api.Application.DTOs.ElectronicBilling;

public sealed class NubeFactGuideDocumentRequest
{
    [JsonPropertyName("operacion")]
    public string Operacion { get; set; } = "generar_comprobante";

    [JsonPropertyName("tipo_de_comprobante")]
    public int TipoDeComprobante { get; set; } = 31;

    [JsonPropertyName("serie")]
    public string Serie { get; set; } = string.Empty;

    [JsonPropertyName("numero")]
    public int Numero { get; set; }

    [JsonPropertyName("fecha_de_emision")]
    public string FechaDeEmision { get; set; } = string.Empty;

    [JsonPropertyName("fecha_de_traslado")]
    public string FechaDeTraslado { get; set; } = string.Empty;

    [JsonPropertyName("motivo_de_traslado")]
    public string MotivoDeTraslado { get; set; } = "01";

    [JsonPropertyName("indicador_de_transbordo")]
    public bool IndicadorDeTransbordo { get; set; }

    [JsonPropertyName("modalidad_de_traslado")]
    public string ModalidadDeTraslado { get; set; } = "01";

    [JsonPropertyName("peso_bruto_total")]
    public decimal PesoBrutoTotal { get; set; }

    [JsonPropertyName("numero_de_bultos")]
    public int NumeroDeBultos { get; set; } = 1;

    [JsonPropertyName("unidad_de_peso")]
    public string UnidadDePeso { get; set; } = "KGM";

    [JsonPropertyName("numero_de_contenedor")]
    public string? NumeroDeContenedor { get; set; }

    // Destinatario (recipient)
    [JsonPropertyName("destinatario_tipo_de_documento")]
    public int DestinatarioTipoDeDocumento { get; set; } = 1;

    [JsonPropertyName("destinatario_numero_de_documento")]
    public string DestinatarioNumeroDeDocumento { get; set; } = string.Empty;

    [JsonPropertyName("destinatario_denominacion")]
    public string DestinatarioDenominacion { get; set; } = string.Empty;

    [JsonPropertyName("destinatario_direccion")]
    public string? DestinatarioDireccion { get; set; }

    [JsonPropertyName("destinatario_email")]
    public string? DestinatarioEmail { get; set; }

    // Departure point (origin)
    [JsonPropertyName("codigo_de_ubigeo_de_partida")]
    public string? CodigoDeUbigeoDePartida { get; set; }

    [JsonPropertyName("direccion_de_partida")]
    public string? DireccionDePartida { get; set; }

    // Arrival point (destination)
    [JsonPropertyName("codigo_de_ubigeo_de_llegada")]
    public string? CodigoDeUbigeoDeLlegada { get; set; }

    [JsonPropertyName("direccion_de_llegada")]
    public string? DireccionDeLlegada { get; set; }

    // Carrier (for public transport mode "02")
    [JsonPropertyName("transportista_tipo_de_documento")]
    public int? TransportistaTipoDeDocumento { get; set; }

    [JsonPropertyName("transportista_numero_de_documento")]
    public string? TransportistaNumeroDeDocumento { get; set; }

    [JsonPropertyName("transportista_denominacion")]
    public string? TransportistaDenominacion { get; set; }

    [JsonPropertyName("transportista_placa_numero")]
    public string? TransportistaPlacaNumero { get; set; }

    // Driver
    [JsonPropertyName("conductor_tipo_de_documento")]
    public int? ConductorTipoDeDocumento { get; set; }

    [JsonPropertyName("conductor_numero_de_documento")]
    public string? ConductorNumeroDeDocumento { get; set; }

    [JsonPropertyName("conductor_denominacion")]
    public string? ConductorDenominacion { get; set; }

    [JsonPropertyName("conductor_numero_licencia")]
    public string? ConductorNumeroLicencia { get; set; }

    [JsonPropertyName("enviar_automaticamente_a_la_sunat")]
    public bool EnviarAutomaticamenteALaSunat { get; set; } = true;

    [JsonPropertyName("formato_de_pdf")]
    public string FormatoDePdf { get; set; } = "A4";

    [JsonPropertyName("items")]
    public IReadOnlyList<NubeFactGuideItemRequest> Items { get; set; } = Array.Empty<NubeFactGuideItemRequest>();
}
