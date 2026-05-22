namespace AdminHiitop.Api.Application.DTOs.Orders;

public sealed class OrderItemUpsertRequest
{
    public int? ProductId { get; set; }
    public int? ColorId { get; set; }
    public int? CollectionId { get; set; }
    public string? ProductDescription { get; set; }
    public string? ProductKey { get; set; }
    public string? TrackingNumber { get; set; }
    public string? Size { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
}
