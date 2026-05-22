using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Application.DTOs.Pos;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IPosService
{
    Task<PosInitialDataResponse> GetInitialDataAsync();
    Task<Order> CreateOrderAsync(PosOrderCreateRequest request, int? userId = null);
}
