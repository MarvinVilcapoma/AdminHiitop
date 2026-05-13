using System.Text.Json.Serialization;

namespace AdminHiitop.Api.Application.DTOs.ElectronicBilling;

public sealed class NubeFactItemRequest
{
    [JsonPropertyName("unidad_de_medida")]
    public string UnidadDeMedida { get; set; } = "NIU";

    [JsonPropertyName("codigo")]
    public string Codigo { get; set; } = string.Empty;

    [JsonPropertyName("descripcion")]
    public string Descripcion { get; set; } = string.Empty;

    [JsonPropertyName("cantidad")]
    public decimal Cantidad { get; set; }

    [JsonPropertyName("valor_unitario")]
    public decimal ValorUnitario { get; set; }

    [JsonPropertyName("precio_unitario")]
    public decimal PrecioUnitario { get; set; }

    [JsonPropertyName("descuento")]
    public decimal? Descuento { get; set; }

    [JsonPropertyName("subtotal")]
    public decimal Subtotal { get; set; }

    [JsonPropertyName("tipo_de_igv")]
    public int TipoDeIgv { get; set; } = 1;

    [JsonPropertyName("igv")]
    public decimal Igv { get; set; }

    [JsonPropertyName("total")]
    public decimal Total { get; set; }

    [JsonPropertyName("anticipo_regularizacion")]
    public bool AnticipoRegularizacion { get; set; }

    [JsonPropertyName("anticipo_comprobante_serie")]
    public string? AnticipoComprobanteSerie { get; set; }

    [JsonPropertyName("anticipo_comprobante_numero")]
    public string? AnticipoComprobanteNumero { get; set; }
}
