using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface ICollectionService
{
    Task<object> GetAsync(int perPage, int page, string? search);
    Task<Collection?> GetByIdAsync(int id);
    Task<Collection> CreateAsync(Collection request);
    Task<Collection> UpdateAsync(int id, Collection request);
    Task DeleteAsync(int id);
}
