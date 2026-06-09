using AdminHiitop.Api.Application.DTOs.Finance;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IInvestmentService
{
    Task<List<InvestmentCategoryResponse>> GetCategoriesAsync();
    Task<List<InvestmentResponse>> GetAllAsync();
    Task<InvestmentResponse> CreateAsync(CreateInvestmentRequest request, int? userId = null);
    Task<InvestmentResponse> UpdateAsync(int id, UpdateInvestmentRequest request, int? userId = null);
    Task DeleteAsync(int id);
}
