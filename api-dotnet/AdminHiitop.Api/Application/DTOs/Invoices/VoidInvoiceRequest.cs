namespace AdminHiitop.Api.Application.DTOs.Invoices;

public sealed class VoidInvoiceRequest
{
    /// <summary>
    /// "baja"         → comunicar_baja in Nubefact (only valid within 7 days).
    /// "credit_note"  → Emit Nota de Crédito (always available).
    /// "auto"         → System decides: baja if within 7 days, NC otherwise.
    /// </summary>
    public string VoidMethod { get; set; } = "auto";

    /// <summary>Free-text reason for the baja. Required when VoidMethod = "baja".</summary>
    public string? Motivo { get; set; }

    /// <summary>SUNAT credit note reason code. '01' = Anulación, '06' = Devolución total, etc.</summary>
    public string NoteMotive { get; set; } = "01";
    public string? NoteMotiveDesc { get; set; }

    /// <summary>When true, the credit note is sent to SUNAT/Nubefact automatically.</summary>
    public bool AutoSend { get; set; } = true;
}
