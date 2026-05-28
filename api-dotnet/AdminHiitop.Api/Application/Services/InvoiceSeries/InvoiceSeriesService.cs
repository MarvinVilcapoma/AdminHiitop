using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
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
        entity.DocType    = string.IsNullOrWhiteSpace(request.DocType) ? entity.DocType : request.DocType;
        entity.Serie      = string.IsNullOrWhiteSpace(request.Serie)   ? entity.Serie   : request.Serie;
        entity.Name       = string.IsNullOrWhiteSpace(request.Name)    ? entity.Name    : request.Name;
        entity.NextNumber = request.NextNumber == 0 ? entity.NextNumber : request.NextNumber;
        entity.IsActive   = request.IsActive;
        await context.SaveChangesAsync();
        return entity;
    }

    public async Task<(string Serie, int Correlativo)> GetNextAsync(int seriesId)
    {
        InvoiceSeriesEntity series = await FindAsync(seriesId);
        return await ReserveNextAsync(series);
    }

    public async Task<(string Serie, int Correlativo)> GetNextAsync(string docType, string serie)
    {
        InvoiceSeriesEntity series = await context.InvoiceSeries
            .FirstOrDefaultAsync(s => s.DocType == docType && s.Serie == serie && s.IsActive)
            ?? throw new AppException($"No existe serie activa '{serie}' para el tipo de documento '{docType}'.", 404);

        return await ReserveNextAsync(series);
    }

    private async Task<(string Serie, int Correlativo)> ReserveNextAsync(InvoiceSeriesEntity series)
    {
        // Atomic increment: LAST_INSERT_ID(expr) sets and returns expr before +1 takes effect,
        // so LAST_INSERT_ID() after the UPDATE holds the old NextNumber (= the correlativo to use).
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE `InvoiceSeries` SET `NextNumber` = LAST_INSERT_ID(`NextNumber`) + 1 WHERE `Id` = {0}",
            series.Id);

        long correlativo = await context.Database
            .SqlQueryRaw<long>("SELECT LAST_INSERT_ID() AS `Value`")
            .SingleAsync();

        return (series.Serie, (int)correlativo);
    }

    private async Task<InvoiceSeriesEntity> FindAsync(int id)
        => await context.InvoiceSeries.FirstOrDefaultAsync(item => item.Id == id)
           ?? throw new AppException("Serie de comprobante no encontrada.", 404);
}
