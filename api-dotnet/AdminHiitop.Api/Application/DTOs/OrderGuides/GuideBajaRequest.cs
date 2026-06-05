namespace AdminHiitop.Api.Application.DTOs.OrderGuides;

public sealed class GuideBajaRequest
{
    /// <summary>True if the transport has already started.</summary>
    public bool TransferStarted { get; set; }

    /// <summary>True if the goods have already arrived at destination.</summary>
    public bool GoodsArrived { get; set; }

    /// <summary>Reason for the baja, sent to SUNAT via Nubefact.</summary>
    public string Motivo { get; set; } = string.Empty;
}
