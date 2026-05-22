namespace AdminHiitop.Api.Application.DTOs.Orders;

public sealed class OrderUpsertRequest
{
    public string? OrderNumber { get; set; }
    public DateTime OrderDate { get; set; }
    public int OrderStatusId { get; set; }
    public int? ShippingAgencyId { get; set; }
    public int? PurchaseTypeId { get; set; }
    public int? WarehouseId { get; set; }
    public string? Observations { get; set; }
    public string? Phone { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? Dni { get; set; }
    public int? ProvinceId { get; set; }
    public int? DistrictId { get; set; }
    public string? Address { get; set; }
    public string? PickupKey { get; set; }
    public string? TrackingNumber { get; set; }
    public decimal DeliveryCost { get; set; }
    public decimal Total { get; set; }
    public int? DocumentTypeId { get; set; }
    public int? DocumentPrintFormatId { get; set; }
    public string? DocumentNumber { get; set; }
    public string? CustomerEmail { get; set; }
    public bool NeedsReceipt { get; set; }
    public int? UserId { get; set; }
    public string? GuideTransferReasonCode { get; set; }
    public string? GuideTransferReasonDescription { get; set; }
    public string? GuideTransferMode { get; set; }
    public DateTime? GuideTransferDate { get; set; }
    public decimal? GuideTotalWeight { get; set; }
    public string? GuideWeightUnit { get; set; }
    public int? GuidePackageCount { get; set; }
    public string? GuideOriginUbigeo { get; set; }
    public string? GuideOriginAddress { get; set; }
    public string? GuideDestinationUbigeo { get; set; }
    public string? GuideDestinationAddress { get; set; }
    public string? GuideRecipientDocType { get; set; }
    public string? GuideRecipientDocNumber { get; set; }
    public string? GuideRecipientName { get; set; }
    public string? GuideCarrierDocType { get; set; }
    public string? GuideCarrierDocNumber { get; set; }
    public string? GuideCarrierName { get; set; }
    public string? GuideVehiclePlate { get; set; }
    public string? GuideDriverDocType { get; set; }
    public string? GuideDriverDocNumber { get; set; }
    public string? GuideDriverName { get; set; }
    public string? GuideDriverLicense { get; set; }
    public string? GuideTransportCertificate { get; set; }
    public List<OrderItemUpsertRequest> Items { get; set; } = new();
}
