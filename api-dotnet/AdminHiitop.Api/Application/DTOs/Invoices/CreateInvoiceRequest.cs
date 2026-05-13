namespace AdminHiitop.Api.Application.DTOs.Invoices;

public sealed class CreateInvoiceRequest
{
    public int? OrderId { get; init; }
    public int InvoiceSeriesId { get; init; }
    public string? DocType { get; init; }
    public string? FormOfPayment { get; init; }
    public int? PaymentMethodId { get; init; }
    public string? CustomerDocType { get; init; }
    public string? CustomerDocNumber { get; init; }
    public string? CustomerName { get; init; }
    public bool AutoSend { get; init; }
}
