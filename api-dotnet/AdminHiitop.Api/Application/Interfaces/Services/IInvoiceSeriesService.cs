using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IInvoiceSeriesService
{
    Task<object> GetAsync(int perPage, int page);
    Task<InvoiceSeries> CreateAsync(InvoiceSeries request);
    Task<InvoiceSeries> UpdateAsync(int id, InvoiceSeries request);
}
