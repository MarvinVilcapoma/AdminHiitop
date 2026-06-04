using System.Text.Json.Serialization;

namespace AdminHiitop.Api.Application.DTOs.ElectronicBilling;

/// <summary>
/// Payload for NubeFact "generar_guia" operation.
/// tipo_de_comprobante: 7 = GRE Remitente, 8 = GRE Transportista.
/// </summary>
public sealed class NubeFactGuideDocumentRequest
{
    [JsonPropertyName("operacion")]
    public string Operacion { get; set; } = "generar_guia";

    [JsonPropertyName("tipo_de_comprobante")]
    public int TipoDeComprobante { get; set; } = 7;

    [JsonPropertyName("serie")]
    public string Serie { get; set; } = string.Empty;

    [JsonPropertyName("numero")]
    public int Numero { get; set; }

    /// <summary>
    /// For GRE Remitente: the destinatario (who receives goods).
    /// For GRE Transportista: the remitente (who sends goods).
    /// </summary>
    [JsonPropertyName("cliente_tipo_de_documento")]
    public int ClienteTipoDeDocumento { get; set; } = 1;

    [JsonPropertyName("cliente_numero_de_documento")]
    public string ClienteNumeroDeDocumento { get; set; } = string.Empty;

    [JsonPropertyName("cliente_denominacion")]
    public string ClienteDenominacion { get; set; } = string.Empty;

    [JsonPropertyName("cliente_direccion")]
    public string? ClienteDireccion { get; set; }

    [JsonPropertyName("cliente_email")]
    public string? ClienteEmail { get; set; }

    [JsonPropertyName("fecha_de_emision")]
    public string FechaDeEmision { get; set; } = string.Empty;

    [JsonPropertyName("observaciones")]
    public string? Observaciones { get; set; }

    // ── GRE Remitente only ───────────────────────────────────────────────────

    /// <summary>01=Venta 02=Compra 04=Traslado 06=Devolucion 13=Otros (GRE Remitente only)</summary>
    [JsonPropertyName("motivo_de_traslado")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MotivoDeTraslado { get; set; }

    /// <summary>01=Transporte público, 02=Transporte privado (GRE Remitente only)</summary>
    [JsonPropertyName("tipo_de_transporte")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TipoDeTransporte { get; set; }

    [JsonPropertyName("numero_de_bultos")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? NumeroDeBultos { get; set; }

    // ── Both types ───────────────────────────────────────────────────────────

    [JsonPropertyName("peso_bruto_total")]
    public decimal PesoBrutoTotal { get; set; } = 1m;

    [JsonPropertyName("peso_bruto_unidad_de_medida")]
    public string PesoBrutoUnidadDeMedida { get; set; } = "KGM";

    [JsonPropertyName("fecha_de_inicio_de_traslado")]
    public string FechaDeInicioDeTraslado { get; set; } = string.Empty;

    /// <summary>Required when tipo_de_transporte = "01" (public transport)</summary>
    [JsonPropertyName("fecha_de_entrega_al_transportista")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FechaDeEntregaAlTransportista { get; set; }

    // ── Carrier (transportista) — GRE Remitente public transport ─────────────

    [JsonPropertyName("transportista_documento_tipo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TransportistaDocumentoTipo { get; set; }

    [JsonPropertyName("transportista_documento_numero")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TransportistaDocumentoNumero { get; set; }

    [JsonPropertyName("transportista_denominacion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TransportistaDenominacion { get; set; }

    [JsonPropertyName("transportista_placa_numero")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TransportistaPlacaNumero { get; set; }

    // ── Driver (conductor) ───────────────────────────────────────────────────

    [JsonPropertyName("conductor_documento_tipo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ConductorDocumentoTipo { get; set; }

    [JsonPropertyName("conductor_documento_numero")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ConductorDocumentoNumero { get; set; }

    [JsonPropertyName("conductor_nombre")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ConductorNombre { get; set; }

    [JsonPropertyName("conductor_apellidos")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ConductorApellidos { get; set; }

    [JsonPropertyName("conductor_numero_licencia")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ConductorNumeroLicencia { get; set; }

    // ── GRE Transportista: separate destinatario ─────────────────────────────

    [JsonPropertyName("destinatario_documento_tipo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DestinatarioDocumentoTipo { get; set; }

    [JsonPropertyName("destinatario_documento_numero")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DestinatarioDocumentoNumero { get; set; }

    [JsonPropertyName("destinatario_denominacion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DestinatarioDenominacion { get; set; }

    // ── Origin / Destination ─────────────────────────────────────────────────

    [JsonPropertyName("punto_de_partida_ubigeo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PuntoDePartidaUbigeo { get; set; }

    [JsonPropertyName("punto_de_partida_direccion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PuntoDePartidaDireccion { get; set; }

    [JsonPropertyName("punto_de_partida_codigo_establecimiento_sunat")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PuntoDePartidaCodigoEstablecimientoSunat { get; set; }

    [JsonPropertyName("punto_de_llegada_ubigeo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PuntoDeLlegadaUbigeo { get; set; }

    [JsonPropertyName("punto_de_llegada_direccion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PuntoDeLlegadaDireccion { get; set; }

    [JsonPropertyName("punto_de_llegada_codigo_establecimiento_sunat")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PuntoDeLlegadaCodigoEstablecimientoSunat { get; set; }

    [JsonPropertyName("enviar_automaticamente_al_cliente")]
    public string EnviarAutomaticamenteAlCliente { get; set; } = "false";

    [JsonPropertyName("formato_de_pdf")]
    public string FormatoDePdf { get; set; } = "";

    [JsonPropertyName("items")]
    public IReadOnlyList<NubeFactGuideItemRequest> Items { get; set; } = Array.Empty<NubeFactGuideItemRequest>();

    [JsonPropertyName("documento_relacionado")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<NubeFactGuideRelatedDoc>? DocumentoRelacionado { get; set; }
}

public sealed class NubeFactGuideRelatedDoc
{
    [JsonPropertyName("tipo")]
    public string Tipo { get; set; } = "01";

    [JsonPropertyName("serie")]
    public string Serie { get; set; } = string.Empty;

    [JsonPropertyName("numero")]
    public int Numero { get; set; }
}
