using AdminHiitop.Api.Application.DTOs.SaleImports;
using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface ISaleImportService
{
    Task<IEnumerable<SaleImport>> GetAsync(CancellationToken cancellationToken);
    Task<object> GetSummaryAsync(CancellationToken cancellationToken);
    Task<IEnumerable<SaleImport>> GetByBatchAsync(string batch, CancellationToken cancellationToken);
    Task<object> ImportAsync(ImportRowsRequest request, CancellationToken cancellationToken);
    Task DeleteBatchAsync(string batch, CancellationToken cancellationToken);
}
