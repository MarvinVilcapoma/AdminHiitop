using AdminHiitop.Api.Application.DTOs.Invoices;
using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public record InvoiceFileContent(byte[] Content, string FileName);

public interface IInvoiceService
{
    Task<object> GetAsync(int perPage, int page);
    Task<object> GetSeriesAsync();
    Task<Invoice?> GetByIdAsync(int id);
    Task<object> CreateAsync(CreateInvoiceRequest request);
    Task<object> TestConnectionAsync();
    Task<object> SendAsync(int id);
    Task<object> VoidAsync(int id);
    Task<InvoiceFileContent?> GetXmlAsync(int id);
    Task<InvoiceFileContent?> GetCdrAsync(int id);
    Task<InvoiceFileContent?> GetPdfAsync(int id);
}
