using AdminHiitop.Api.Application.DTOs.Finance;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IFixedFinancialMovementService
{
    Task<List<FixedFinancialMovementResponse>> GetAllAsync(string? type = null, bool? isActive = null);
    Task<FixedFinancialMovementResponse?> GetByIdAsync(int id);
    Task<FixedFinancialMovementResponse> CreateAsync(FixedFinancialMovementRequest request, int? userId);
    Task<FixedFinancialMovementResponse> UpdateAsync(int id, FixedFinancialMovementRequest request, int? userId);
    Task DeleteAsync(int id);
    /// <summary>Generates FinancialMovements for all active fixed movements that match the given month/year.</summary>
    Task<int> GenerateMonthAsync(int year, int month, int? userId);
}
