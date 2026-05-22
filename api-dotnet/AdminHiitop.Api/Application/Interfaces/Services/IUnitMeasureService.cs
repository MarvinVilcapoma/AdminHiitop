using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IUnitMeasureService
{
    Task<object> GetAsync(int perPage, int page, string? search);
    Task<UnitMeasure?> GetByIdAsync(int id);
    Task<UnitMeasure> CreateAsync(UnitMeasure request);
    Task<UnitMeasure> UpdateAsync(int id, UnitMeasure request);
    Task DeleteAsync(int id);
}
