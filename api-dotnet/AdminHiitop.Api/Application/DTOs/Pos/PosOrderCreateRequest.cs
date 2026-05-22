using AdminHiitop.Api.Application.DTOs.Orders;

namespace AdminHiitop.Api.Application.DTOs.Pos;

public sealed class PosOrderCreateRequest
{
    public DateTime OrderDate { get; set; }
    public int WarehouseId { get; set; }
    public int PaymentMethodId { get; set; }
    public int DocumentTypeId { get; set; }
    public int? DocumentPrintFormatId { get; set; }
    public int? OrderStatusId { get; set; }
    public string? Observations { get; set; }
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerDocument { get; set; }
    public string? CustomerDocumentType { get; set; }
    public string? CustomerEmail { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool PrintAfterSave { get; set; }
    public List<OrderItemUpsertRequest> Items { get; set; } = new();
}
