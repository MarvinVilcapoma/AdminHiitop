using AdminHiitop.Api.Application.DTOs.Invoices;
using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public record InvoiceFileContent(byte[] Content, string FileName);

public interface IInvoiceService
{
    Task<object> GetAsync(int perPage, int page, CancellationToken cancellationToken);
    Task<object> GetSeriesAsync(CancellationToken cancellationToken);
    Task<Invoice?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<object> CreateAsync(CreateInvoiceRequest request, CancellationToken cancellationToken);
    Task<object> TestConnectionAsync();
    Task<object> SendAsync(int id);
    Task<object> VoidAsync(int id, CancellationToken cancellationToken);
    Task<InvoiceFileContent?> GetXmlAsync(int id, CancellationToken cancellationToken);
    Task<InvoiceFileContent?> GetCdrAsync(int id, CancellationToken cancellationToken);
    Task<InvoiceFileContent?> GetPdfAsync(int id, CancellationToken cancellationToken);
}
