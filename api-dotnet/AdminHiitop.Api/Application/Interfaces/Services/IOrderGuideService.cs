using AdminHiitop.Api.Application.DTOs.Common;
using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IOrderGuideService
{
    Task<IReadOnlyList<Order>> GetGuidesAsync();
    Task<Order?> GetByOrderIdAsync(int orderId);
    Task<object?> SendAsync(int orderId);
    Task<object?> ConsultAsync(int orderId);
    Task<FileDownloadResponse?> GetXmlAsync(int orderId);
    Task<FileDownloadResponse?> GetCdrAsync(int orderId);
    Task<FileDownloadResponse?> GetPdfAsync(int orderId);
    Task<object> GetWhatsAppLinkAsync(int orderId, string phone, string countryCode = "51");
    Task<object> SendEmailAsync(int orderId, string email);
}
