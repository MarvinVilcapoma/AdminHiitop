namespace AdminHiitop.Api.Application.DTOs.Common;

public sealed class PosOptions
{
    public const string SectionName = "Pos";
    public int MaxInvoiceSeries { get; set; } = 10;
}
