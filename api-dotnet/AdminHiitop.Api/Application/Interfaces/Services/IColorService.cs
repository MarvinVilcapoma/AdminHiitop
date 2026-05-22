using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IColorService
{
    Task<object> GetAsync(int perPage, int page, string? search);
    Task<Color?> GetByIdAsync(int id);
    Task<Color> CreateAsync(Color request);
    Task<Color> UpdateAsync(int id, Color request);
    Task DeleteAsync(int id);
}
