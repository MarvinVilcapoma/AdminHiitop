using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface ISaleService
{
    Task<object> GetAsync(int perPage, int page, CancellationToken cancellationToken);
    IEnumerable<string> GetBranches();
    Task<Sale?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<Sale> CreateAsync(Sale request, CancellationToken cancellationToken);
    Task<Sale> UpdateAsync(int id, Sale request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
