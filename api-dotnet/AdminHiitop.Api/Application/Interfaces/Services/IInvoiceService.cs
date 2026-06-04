using AdminHiitop.Api.Application.DTOs.Invoices;
using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

/// <summary>When RedirectUrl is set, redirect there instead of serving Content.</summary>
public record InvoiceFileContent(byte[] Content, string FileName, string? RedirectUrl = null);

public interface IInvoiceService
{
    Task<object> GetAsync(int perPage, int page);
    Task<object> GetSeriesAsync();
    Task<Invoice?> GetByIdAsync(int id);
    Task<object> CreateAsync(CreateInvoiceRequest request);
    Task<object> TestConnectionAsync();
    Task<object> SendAsync(int id);
    Task<object> GetVoidCheckAsync(int id);
    Task<object> VoidAsync(int id, VoidInvoiceRequest request);
    Task<InvoiceFileContent?> GetXmlAsync(int id);
    Task<InvoiceFileContent?> GetCdrAsync(int id);
    Task<InvoiceFileContent?> GetPdfAsync(int id);
}
