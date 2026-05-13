using System.Text.Json.Serialization;

namespace AdminHiitop.Api.Application.DTOs.ElectronicBilling;

public sealed class NubeFactDocumentRequest
{
    [JsonPropertyName("operacion")]
    public string Operacion { get; set; } = "generar_comprobante";

    [JsonPropertyName("tipo_de_comprobante")]
    public int TipoDeComprobante { get; set; }

    [JsonPropertyName("serie")]
    public string Serie { get; set; } = string.Empty;

    [JsonPropertyName("numero")]
    public int Numero { get; set; }

    [JsonPropertyName("sunat_transaction")]
    public int SunatTransaction { get; set; } = 1;

    [JsonPropertyName("cliente_tipo_de_documento")]
    public int ClienteTipoDeDocumento { get; set; }

    [JsonPropertyName("cliente_numero_de_documento")]
    public string ClienteNumeroDeDocumento { get; set; } = string.Empty;

    [JsonPropertyName("cliente_denominacion")]
    public string ClienteDenominacion { get; set; } = string.Empty;

    [JsonPropertyName("cliente_direccion")]
    public string ClienteDireccion { get; set; } = string.Empty;

    [JsonPropertyName("cliente_email")]
    public string ClienteEmail { get; set; } = string.Empty;

    [JsonPropertyName("cliente_email_1")]
    public string ClienteEmail1 { get; set; } = string.Empty;

    [JsonPropertyName("cliente_email_2")]
    public string ClienteEmail2 { get; set; } = string.Empty;

    [JsonPropertyName("fecha_de_emision")]
    public string FechaDeEmision { get; set; } = string.Empty;

    [JsonPropertyName("fecha_de_vencimiento")]
    public string? FechaDeVencimiento { get; set; }

    [JsonPropertyName("moneda")]
    public int Moneda { get; set; } = 1;

    [JsonPropertyName("tipo_de_cambio")]
    public string? TipoDeCambio { get; set; }

    [JsonPropertyName("porcentaje_de_igv")]
    public decimal PorcentajeDeIgv { get; set; } = 18.00m;

    [JsonPropertyName("descuento_global")]
    public decimal? DescuentoGlobal { get; set; }

    [JsonPropertyName("total_descuento")]
    public decimal? TotalDescuento { get; set; }

    [JsonPropertyName("total_anticipo")]
    public decimal? TotalAnticipo { get; set; }

    [JsonPropertyName("total_gravada")]
    public decimal TotalGravada { get; set; }

    [JsonPropertyName("total_inafecta")]
    public decimal? TotalInafecta { get; set; }

    [JsonPropertyName("total_exonerada")]
    public decimal? TotalExonerada { get; set; }

    [JsonPropertyName("total_igv")]
    public decimal TotalIgv { get; set; }

    [JsonPropertyName("total_gratuita")]
    public decimal? TotalGratuita { get; set; }

    [JsonPropertyName("total_otros_cargos")]
    public decimal? TotalOtrosCargos { get; set; }

    [JsonPropertyName("total")]
    public decimal Total { get; set; }

    [JsonPropertyName("percepcion_tipo")]
    public string? PercepcionTipo { get; set; }

    [JsonPropertyName("percepcion_base_imponible")]
    public decimal? PercepcionBaseImponible { get; set; }

    [JsonPropertyName("total_percepcion")]
    public decimal? TotalPercepcion { get; set; }

    [JsonPropertyName("total_incluido_percepcion")]
    public decimal? TotalIncluidoPercepcion { get; set; }

    [JsonPropertyName("detraccion")]
    public bool Detraccion { get; set; }

    [JsonPropertyName("observaciones")]
    public string Observaciones { get; set; } = string.Empty;

    [JsonPropertyName("documento_que_se_modifica_tipo")]
    public string? DocumentoQueSeModificaTipo { get; set; }

    [JsonPropertyName("documento_que_se_modifica_serie")]
    public string? DocumentoQueSeModificaSerie { get; set; }

    [JsonPropertyName("documento_que_se_modifica_numero")]
    public string? DocumentoQueSeModificaNumero { get; set; }

    [JsonPropertyName("tipo_de_nota_de_credito")]
    public string? TipoDeNotaDeCredito { get; set; }

    [JsonPropertyName("tipo_de_nota_de_debito")]
    public string? TipoDeNotaDeDebito { get; set; }

    [JsonPropertyName("enviar_automaticamente_a_la_sunat")]
    public bool EnviarAutomaticamenteALaSunat { get; set; } = true;

    [JsonPropertyName("enviar_automaticamente_al_cliente")]
    public bool EnviarAutomaticamenteAlCliente { get; set; }

    [JsonPropertyName("codigo_unico")]
    public string? CodigoUnico { get; set; }

    [JsonPropertyName("condiciones_de_pago")]
    public string? CondicionesDePago { get; set; }

    [JsonPropertyName("medio_de_pago")]
    public string? MedioDePago { get; set; }

    [JsonPropertyName("placa_vehiculo")]
    public string? PlacaVehiculo { get; set; }

    [JsonPropertyName("orden_compra_servicio")]
    public string? OrdenCompraServicio { get; set; }

    [JsonPropertyName("tabla_personalizada_codigo")]
    public string? TablaPersonalizadaCodigo { get; set; }

    [JsonPropertyName("formato_de_pdf")]
    public string? FormatoDePdf { get; set; }

    [JsonPropertyName("items")]
    public IReadOnlyList<NubeFactItemRequest> Items { get; set; } = Array.Empty<NubeFactItemRequest>();

    [JsonPropertyName("guias")]
    public IReadOnlyList<NubeFactGuideRequest> Guias { get; set; } = Array.Empty<NubeFactGuideRequest>();
}
