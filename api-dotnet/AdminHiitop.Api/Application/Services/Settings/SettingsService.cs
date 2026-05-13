using AdminHiitop.Api.Application.DTOs.Settings;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Settings;

public sealed class SettingsService : ISettingsService
{
    private readonly AdminHiitopDbContext _context;

    public SettingsService(AdminHiitopDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SettingResponse>> GetAsync(string? group)
    {
        IQueryable<Setting> query = _context.Settings.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(group))
        {
            string normalizedGroup = group.Trim();
            query = query.Where(item => item.Group == normalizedGroup);
        }

        return await query
            .OrderBy(item => item.Group)
            .ThenBy(item => item.Key)
            .Select(item => new SettingResponse
            {
                Key = item.Key,
                Value = item.Value,
                Label = item.Label,
                Type = item.Type,
                Group = item.Group
            })
            .ToListAsync();
    }

    public async Task<SettingResponse> UpsertAsync(string key, UpdateSettingRequest request)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new AppException("La clave de configuración es obligatoria.");
        }

        if (request is null)
        {
            throw new AppException("La solicitud de configuración es obligatoria.");
        }

        string normalizedKey = key.Trim();
        Setting? setting = await _context.Settings.FirstOrDefaultAsync(item => item.Key == normalizedKey);

        if (setting is null)
        {
            setting = new Setting
            {
                Key = normalizedKey,
                Value = request.Value,
                Label = request.Label,
                Type = string.IsNullOrWhiteSpace(request.Type) ? "string" : request.Type.Trim(),
                Group = string.IsNullOrWhiteSpace(request.Group) ? "general" : request.Group.Trim()
            };

            _context.Settings.Add(setting);
        }
        else
        {
            setting.Value = request.Value;
            setting.Label = request.Label ?? setting.Label;
            setting.Type = string.IsNullOrWhiteSpace(request.Type) ? setting.Type : request.Type.Trim();
            setting.Group = string.IsNullOrWhiteSpace(request.Group) ? setting.Group : request.Group.Trim();
        }

        await _context.SaveChangesAsync();

        return new SettingResponse
        {
            Key = setting.Key,
            Value = setting.Value,
            Label = setting.Label,
            Type = setting.Type,
            Group = setting.Group
        };
    }
}
