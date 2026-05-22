namespace AdminHiitop.Api.Infrastructure.ElectronicBilling;

public sealed class DocumentDefaultsOptions
{
    public const string SectionName = "ElectronicBilling:DocumentDefaults";

    public string DefaultInvoiceSeries { get; set; } = "F001";
    public string DefaultReceiptSeries { get; set; } = "B001";
    public string DefaultCreditNoteSeries { get; set; } = "FC01";
    public string DefaultDebitNoteSeries { get; set; } = "FD01";
    public int IgvPercentage { get; set; } = 18;
    public bool SendAutomatically { get; set; } = false;
}
