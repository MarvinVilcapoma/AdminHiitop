using AdminHiitop.Api.Application.DTOs.Settings;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface ISettingsService
{
    Task<IReadOnlyList<SettingResponse>> GetAsync(string? group);
    Task<SettingResponse> UpsertAsync(string key, UpdateSettingRequest request);
}
