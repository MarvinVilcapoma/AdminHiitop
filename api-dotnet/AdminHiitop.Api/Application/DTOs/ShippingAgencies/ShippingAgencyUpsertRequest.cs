using System.Text.Json;

namespace AdminHiitop.Api.Application.DTOs.ShippingAgencies;

public sealed class ShippingAgencyUpsertRequest
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public JsonElement? ShippingRate { get; set; }
    public bool? IsActive { get; set; }
}
