using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface ICollectionService
{
    Task<object> GetAsync(int perPage, int page, string? search, CancellationToken cancellationToken);
    Task<Collection?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<Collection> CreateAsync(Collection request, CancellationToken cancellationToken);
    Task<Collection> UpdateAsync(int id, Collection request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
