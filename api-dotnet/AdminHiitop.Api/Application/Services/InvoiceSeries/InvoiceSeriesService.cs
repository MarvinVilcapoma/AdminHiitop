using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using InvoiceSeriesEntity = AdminHiitop.Api.Domain.Sales.Entities.InvoiceSeries;

namespace AdminHiitop.Api.Application.Services.InvoiceSeries;

public sealed class InvoiceSeriesService(AdminHiitopDbContext context) : IInvoiceSeriesService
{
    public async Task<object> GetAsync(int perPage, int page)
        => await PaginationHelper.CreateAsync(
            context.InvoiceSeries.AsNoTracking().OrderBy(item => item.Serie),
            page,
            perPage);

    public async Task<InvoiceSeriesEntity> CreateAsync(InvoiceSeriesEntity request)
    {
        context.InvoiceSeries.Add(request);
        await context.SaveChangesAsync();
        return request;
    }

    public async Task<InvoiceSeriesEntity> UpdateAsync(int id, InvoiceSeriesEntity request)
    {
        InvoiceSeriesEntity entity = await FindAsync(id);
        entity.DocType     = string.IsNullOrWhiteSpace(request.DocType) ? entity.DocType : request.DocType;
        entity.Serie       = string.IsNullOrWhiteSpace(request.Serie)   ? entity.Serie   : request.Serie;
        entity.NextNumber  = request.NextNumber == 0 ? entity.NextNumber : request.NextNumber;
        entity.IsActive    = request.IsActive;
        await context.SaveChangesAsync();
        return entity;
    }

    private async Task<InvoiceSeriesEntity> FindAsync(int id)
        => await context.InvoiceSeries.FirstOrDefaultAsync(item => item.Id == id)
           ?? throw new AppException("Serie de comprobante no encontrada.", 404);
}
