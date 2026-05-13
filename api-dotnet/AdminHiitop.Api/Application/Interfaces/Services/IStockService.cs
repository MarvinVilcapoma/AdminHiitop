using AdminHiitop.Api.Application.DTOs.Stocks;
using AdminHiitop.Api.Application.DTOs.Common;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IStockService
{
    Task<object> GetAsync(StockQueryRequest request);
    Task<IReadOnlyList<StockSummaryResponse>> GetSummaryAsync();
    Task<IReadOnlyList<StockResponse>> GetAvailableAsync(int? productId);
    Task<IReadOnlyList<StockLookupResponse>> GetLookupAsync(string? search);
    Task<StockResponse> GetByIdAsync(int id);
    Task<StockResponse> CreateAsync(StockUpsertRequest request);
    Task<SuccessResponse> BulkCreateAsync(IReadOnlyList<StockUpsertRequest> request);
    Task<SuccessResponse> BulkTransferAsync(StockBulkTransferRequest request);
    Task<StockResponse> UpdateAsync(int id, StockUpsertRequest request);
    Task DeleteAsync(int id);
    Task<StockResponse> AdjustAsync(int id, StockAdjustRequest request);
    Task<SuccessResponse> TransferAsync(int id, StockTransferRequest request);
}
