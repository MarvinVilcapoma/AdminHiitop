using AdminHiitop.Api.Application.DTOs.Stocks;
using AdminHiitop.Api.Domain.Inventory.Entities;
using AdminHiitop.Api.Shared.Models;

namespace AdminHiitop.Api.Application.Interfaces.Repositories;

public interface IStockRepository
{
    Task<PagedResponse<StockResponse>> GetPagedAsync(StockQueryRequest request);
    Task<IReadOnlyList<StockSummaryResponse>> GetSummaryAsync();
    Task<IReadOnlyList<StockResponse>> GetAvailableAsync(int? productId);
    Task<IReadOnlyList<StockLookupResponse>> GetLookupAsync(string? search);
    Task<Stock?> GetByIdAsync(int id);
    Task<StockResponse?> GetDetailByIdAsync(int id);
    Task<Stock?> FindTransferTargetAsync(int productId, int warehouseId, int? colorId, string? size);
    Task AddAsync(Stock stock);
    Task AddRangeAsync(IReadOnlyCollection<Stock> stocks);
    Task DeleteAsync(Stock stock);
    Task SaveChangesAsync();
}
