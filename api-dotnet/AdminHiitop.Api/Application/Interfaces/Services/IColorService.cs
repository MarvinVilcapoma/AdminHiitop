using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IColorService
{
    Task<object> GetAsync(int perPage, int page, string? search, CancellationToken cancellationToken);
    Task<Color?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<Color> CreateAsync(Color request, CancellationToken cancellationToken);
    Task<Color> UpdateAsync(int id, Color request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
