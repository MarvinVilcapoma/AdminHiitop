using AdminHiitop.Api.Application.DTOs.SaleImports;
using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface ISaleImportService
{
    Task<IEnumerable<SaleImport>> GetAsync();
    Task<object> GetSummaryAsync();
    Task<IEnumerable<SaleImport>> GetByBatchAsync(string batch);
    Task<object> ImportAsync(ImportRowsRequest request);
    Task DeleteBatchAsync(string batch);
}
