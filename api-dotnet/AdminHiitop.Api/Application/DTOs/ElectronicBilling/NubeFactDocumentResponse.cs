using System.Text.Json.Serialization;

namespace AdminHiitop.Api.Application.DTOs.ElectronicBilling;

public sealed class NubeFactDocumentResponse
{
    [JsonPropertyName("errors")]
    public string? Errors { get; set; }

    [JsonPropertyName("tipo")]
    public int Tipo { get; set; }

    [JsonPropertyName("serie")]
    public string? Serie { get; set; }

    [JsonPropertyName("numero")]
    public int? Numero { get; set; }

    // Nubefact devuelve "enlace" (vista del comprobante) y "enlace_del_pdf" (PDF directo).
    [JsonPropertyName("enlace")]
    public string? Url { get; set; }

    [JsonPropertyName("enlace_del_pdf")]
    public string? EnlaceDelPdf { get; set; }

    [JsonPropertyName("enlace_del_xml")]
    public string? EnlaceDelXml { get; set; }

    [JsonPropertyName("enlace_del_cdr")]
    public string? EnlaceDelCdr { get; set; }

    [JsonPropertyName("aceptada_por_sunat")]
    public bool? AceptadaPorSunat { get; set; }

    [JsonPropertyName("sunat_description")]
    public string? SunatDescription { get; set; }

    [JsonPropertyName("sunat_note")]
    public string? SunatNote { get; set; }

    [JsonPropertyName("sunat_responsecode")]
    public string? SunatResponseCode { get; set; }

    [JsonPropertyName("sunat_soap_error")]
    public string? SunatSoapError { get; set; }

    [JsonPropertyName("pdf_zip_base64")]
    public string? PdfZipBase64 { get; set; }

    [JsonPropertyName("xml_zip_base64")]
    public string? XmlZipBase64 { get; set; }

    [JsonPropertyName("cdr_zip_base64")]
    public string? CdrZipBase64 { get; set; }

    [JsonPropertyName("cadena_para_codigo_qr")]
    public string? CadenaParaCodigoQr { get; set; }

    [JsonPropertyName("codigo_hash")]
    public string? CodigoHash { get; set; }

    [JsonPropertyName("codigo_de_barras")]
    public string? CodigoDeBarras { get; set; }
}
