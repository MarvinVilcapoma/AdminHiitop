using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IUnitMeasureService
{
    Task<object> GetAsync(int perPage, int page, string? search, CancellationToken cancellationToken);
    Task<UnitMeasure?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<UnitMeasure> CreateAsync(UnitMeasure request, CancellationToken cancellationToken);
    Task<UnitMeasure> UpdateAsync(int id, UnitMeasure request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
