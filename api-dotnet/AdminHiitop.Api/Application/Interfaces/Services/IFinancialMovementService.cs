using AdminHiitop.Api.Application.DTOs.Finance;
using AdminHiitop.Api.Shared.Models;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IFinancialMovementService
{
    Task<PagedResponse<FinancialMovementResponse>> GetPagedAsync(
        string? type, int? categoryId, string? paymentMethod,
        DateTime? dateFrom, DateTime? dateTo, int? year, int? month,
        int page, int perPage);

    Task<FinancialMovementResponse?> GetByIdAsync(int id);
    Task<FinancialMovementResponse> CreateAsync(FinancialMovementRequest request, int? userId);
    Task<FinancialMovementResponse> UpdateAsync(int id, FinancialMovementRequest request, int? userId);
    Task DeleteAsync(int id);
}
