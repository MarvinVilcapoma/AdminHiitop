using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IProvinceService
{
    Task<object> GetAsync(int perPage, int page, string? search);
    Task<Province?> GetByIdAsync(int id);
    Task<Province> CreateAsync(Province request);
    Task<Province> UpdateAsync(int id, Province request);
    Task DeleteAsync(int id);
}
