using System.Globalization;
using System.Text.Json;
using AdminHiitop.Api.Application.DTOs.ShippingAgencies;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Shared.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[ApiController]
[Route("api/shipping-agencies")]
public sealed class ShippingAgenciesController : ControllerBase
{
    private readonly IShippingAgencyService _shippingAgencyService;

    public ShippingAgenciesController(IShippingAgencyService shippingAgencyService) => _shippingAgencyService = shippingAgencyService;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery(Name = "per_page")] int perPage = 15,
        [FromQuery] int page = 1,
        [FromQuery] string? search = null)
        => Ok(await _shippingAgencyService.GetAsync(perPage, page, search));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => await _shippingAgencyService.GetByIdAsync(id) is { } entity ? Ok(entity) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ShippingAgencyUpsertRequest request)
        => Ok(await _shippingAgencyService.CreateAsync(MapRequest(request)));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ShippingAgencyUpsertRequest request)
        => Ok(await _shippingAgencyService.UpdateAsync(id, MapRequest(request)));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _shippingAgencyService.DeleteAsync(id);
        return Ok(new { success = true });
    }

    private static ShippingAgency MapRequest(ShippingAgencyUpsertRequest request)
    {
        return new ShippingAgency
        {
            Code = request.Code?.Trim() ?? string.Empty,
            Name = request.Name?.Trim() ?? string.Empty,
            ShippingRate = ParseShippingRate(request.ShippingRate),
            IsActive = request.IsActive ?? true
        };
    }

    private static decimal? ParseShippingRate(JsonElement? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        JsonElement shippingRate = value.Value;
        if (shippingRate.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (shippingRate.ValueKind == JsonValueKind.Number)
        {
            if (shippingRate.TryGetDecimal(out decimal numericValue))
            {
                return numericValue;
            }

            throw new AppException("La tarifa de envio debe ser un numero valido.", 400);
        }

        if (shippingRate.ValueKind == JsonValueKind.String)
        {
            string? raw = shippingRate.GetString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            string normalized = raw.Trim().Replace(',', '.');
            if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed))
            {
                return parsed;
            }
        }

        if (decimal.TryParse(shippingRate.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal fallback))
        {
            return fallback;
        }

        throw new AppException("La tarifa de envio debe ser un numero valido.", 400);
    }
}
