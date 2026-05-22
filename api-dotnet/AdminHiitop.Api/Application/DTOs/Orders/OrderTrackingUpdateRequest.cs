namespace AdminHiitop.Api.Application.DTOs.Orders;

public sealed class OrderTrackingUpdateRequest
{
    public string? PickupKey { get; set; }
    public string? TrackingNumber { get; set; }
}
