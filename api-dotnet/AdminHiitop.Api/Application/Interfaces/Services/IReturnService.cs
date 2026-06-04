using AdminHiitop.Api.Application.DTOs.Returns;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IReturnService
{
    Task<object> GetAllAsync(int perPage, int page, string? search = null);
    Task<ReturnRequestResponse?> GetByIdAsync(int id);
    Task<ReturnRequestResponse> CreateReturnAsync(CreateReturnRequest request);
    Task<ReturnRequestResponse> IssueCreditNoteAsync(int returnRequestId);
    Task<object> CancelReturnAsync(int id, string? reason);
    Task<object> GetCustomerCreditsAsync(int perPage, int page, int? customerId = null);
}
