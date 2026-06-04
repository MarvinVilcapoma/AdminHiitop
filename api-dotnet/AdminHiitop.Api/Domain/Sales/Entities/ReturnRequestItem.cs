using AdminHiitop.Api.Domain.Common;

namespace AdminHiitop.Api.Domain.Sales.Entities;

/// <summary>
/// A single product line in a return or exchange request.
/// Condition codes: NEW, USED, DAMAGED, DEFECTIVE.
/// RestockAction codes: RETURN_TO_STOCK, SEND_TO_REVIEW, DO_NOT_RESTOCK.
/// </summary>
public sealed class ReturnRequestItem : AuditableEntity
{
    public int ReturnRequestId { get; set; }
    public int? OrderItemId { get; set; }
    public int? ProductId { get; set; }
    public int? StockId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public string? ProductDescription { get; set; }

    public string Condition { get; set; } = "USED";
    public string RestockAction { get; set; } = "RETURN_TO_STOCK";
    public string? Reason { get; set; }

    public ReturnRequest ReturnRequest { get; set; } = null!;
    public OrderItem? OrderItem { get; set; }
}
