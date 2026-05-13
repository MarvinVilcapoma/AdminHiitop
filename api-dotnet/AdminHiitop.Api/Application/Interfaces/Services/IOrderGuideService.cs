using AdminHiitop.Api.Application.DTOs.Common;
using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IOrderGuideService
{
    Task<IReadOnlyList<Order>> GetGuidesAsync();
    Task<Order?> GetByOrderIdAsync(int orderId);
    Task<object?> SendAsync(int orderId);
    Task<FileDownloadResponse?> GetXmlAsync(int orderId);
    Task<FileDownloadResponse?> GetCdrAsync(int orderId);
}
