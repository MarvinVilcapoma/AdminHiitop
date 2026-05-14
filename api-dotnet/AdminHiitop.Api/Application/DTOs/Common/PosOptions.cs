namespace AdminHiitop.Api.Application.DTOs.Common;

public sealed class PosOptions
{
    public const string SectionName = "Pos";

    /// <summary>Máximo de almacenes que pueden estar marcados como punto de venta (is_pos = true).</summary>
    public int MaxPosWarehouses { get; set; } = 3;
}
