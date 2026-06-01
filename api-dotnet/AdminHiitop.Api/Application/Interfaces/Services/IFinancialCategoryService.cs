using AdminHiitop.Api.Application.DTOs.Finance;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IFinancialCategoryService
{
    Task<List<FinancialCategoryResponse>> GetAllAsync(string? type = null);
    Task<FinancialCategoryResponse?> GetByIdAsync(int id);
    Task<FinancialCategoryResponse> CreateAsync(FinancialCategoryRequest request);
    Task<FinancialCategoryResponse> UpdateAsync(int id, FinancialCategoryRequest request);
    Task DeleteAsync(int id);
}
