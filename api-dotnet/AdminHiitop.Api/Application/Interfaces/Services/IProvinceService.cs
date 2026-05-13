using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IProvinceService
{
    Task<object> GetAsync(int perPage, int page, CancellationToken cancellationToken);
    Task<Province?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<Province> CreateAsync(Province request, CancellationToken cancellationToken);
    Task<Province> UpdateAsync(int id, Province request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
