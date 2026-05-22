using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface ISaleService
{
    Task<object> GetAsync(int perPage, int page);
    IEnumerable<string> GetBranches();
    Task<Sale?> GetByIdAsync(int id);
    Task<Sale> CreateAsync(Sale request);
    Task<Sale> UpdateAsync(int id, Sale request);
    Task DeleteAsync(int id);
}
