using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IDistrictService
{
    Task<object> GetAsync(int perPage, int page, int? provinceId);
    Task<District?> GetByIdAsync(int id);
    Task<District> CreateAsync(District request);
    Task<District> UpdateAsync(int id, District request);
    Task DeleteAsync(int id);
}
