using AdminHiitop.Api.Application.DTOs.Settings;
using AdminHiitop.Api.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AdminHiitop.Api.Controllers;

[Route("api/settings")]
public sealed class SettingsController : BaseApiController
{
    private readonly ISettingsService _settingsService;

    public SettingsController(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? group)
    {
        IReadOnlyList<SettingResponse> response = await _settingsService.GetAsync(group);
        return Ok(ToSettingsDictionary(response));
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> Put(string key, [FromBody] UpdateSettingRequest request)
    {
        SettingResponse response = await _settingsService.UpsertAsync(key, request);
        return Ok(new
        {
            value = response.Value,
            raw = response.Value,
            label = response.Label,
            type = response.Type,
            group = response.Group
        });
    }

    [HttpPatch]
    public async Task<IActionResult> Patch([FromBody] JsonElement request)
    {
        foreach (UpdateSettingEntry item in ParseSettingsPatchRequest(request))
        {
            await _settingsService.UpsertAsync(item.Key, new UpdateSettingRequest { Value = item.Value });
        }

        IReadOnlyList<SettingResponse> response = await _settingsService.GetAsync(null);
        return Ok(ToSettingsDictionary(response));
    }

    [HttpPut("{key}/value")]
    public async Task<IActionResult> UpdateValueAlias(string key, [FromBody] UpdateSettingRequest request)
    {
        SettingResponse response = await _settingsService.UpsertAsync(key, request);
        return Ok(new
        {
            value = response.Value,
            raw = response.Value,
            label = response.Label,
            type = response.Type,
            group = response.Group
        });
    }

    [HttpPost("sunat/import-p12")]
    public IActionResult ImportP12() => Ok(new { success = true, message = "Importacion de certificado pendiente de implementacion." });

    private static object ToSettingsDictionary(IReadOnlyList<SettingResponse> settings)
    {
        return settings.ToDictionary(
            item => item.Key,
            item => new
            {
                value = item.Value,
                raw = item.Value,
                label = item.Label,
                type = item.Type,
                group = item.Group
            });
    }

    private static IReadOnlyList<UpdateSettingEntry> ParseSettingsPatchRequest(JsonElement request)
    {
        List<UpdateSettingEntry> entries = new();

        if (request.ValueKind == JsonValueKind.Object
            && request.TryGetProperty("settings", out JsonElement settingsElement)
            && settingsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in settingsElement.EnumerateArray())
            {
                if (!item.TryGetProperty("key", out JsonElement keyElement))
                {
                    continue;
                }

                string? key = keyElement.GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                string? value = item.TryGetProperty("value", out JsonElement valueElement)
                    ? valueElement.ValueKind == JsonValueKind.Null ? null : valueElement.ToString()
                    : null;

                entries.Add(new UpdateSettingEntry(key, value));
            }

            return entries;
        }

        if (request.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in request.EnumerateObject())
            {
                entries.Add(new UpdateSettingEntry(
                    property.Name,
                    property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.ToString()));
            }
        }

        return entries;
    }

    private sealed record UpdateSettingEntry(string Key, string? Value);
}
