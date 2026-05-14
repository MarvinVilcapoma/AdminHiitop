using AdminHiitop.Api.Application.DTOs.Common;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.Extensions.Options;
using InvoiceSeriesEntity = AdminHiitop.Api.Domain.Sales.Entities.InvoiceSeries;

namespace AdminHiitop.Api.Application.Services.InvoiceSeries;

public sealed class InvoiceSeriesService : IInvoiceSeriesService
{
    private readonly AdminHiitopDbContext _context;
    private readonly PosOptions _posOptions;

    public InvoiceSeriesService(AdminHiitopDbContext context, IOptions<PosOptions> posOptions)
    {
        _context = context;
        _posOptions = posOptions.Value;
    }

    public async Task<object> GetAsync(int perPage, int page)
    {
        return await PaginationHelper.CreateAsync(
            _context.InvoiceSeries.AsNoTracking().OrderBy(item => item.Serie),
            page,
            perPage);
    }

    public async Task<InvoiceSeriesEntity> CreateAsync(InvoiceSeriesEntity request)
    {
        int current = await _context.InvoiceSeries.CountAsync();
        if (current >= _posOptions.MaxInvoiceSeries)
        {
            throw new AppException(
                $"Se alcanzó el límite de {_posOptions.MaxInvoiceSeries} series de comprobante configuradas. " +
                "Ajusta 'Pos:MaxInvoiceSeries' en appsettings.json para aumentar el límite.", 422);
        }

        _context.InvoiceSeries.Add(request);
        await _context.SaveChangesAsync();
        return request;
    }

    public async Task<InvoiceSeriesEntity> UpdateAsync(int id, InvoiceSeriesEntity request)
    {
        InvoiceSeriesEntity entity = await FindAsync(id);
        entity.DocType = string.IsNullOrWhiteSpace(request.DocType) ? entity.DocType : request.DocType;
        entity.Serie = string.IsNullOrWhiteSpace(request.Serie) ? entity.Serie : request.Serie;
        entity.NextNumber = request.NextNumber == 0 ? entity.NextNumber : request.NextNumber;
        entity.IsActive = request.IsActive;
        await _context.SaveChangesAsync();
        return entity;
    }

    private async Task<InvoiceSeriesEntity> FindAsync(int id)
    {
        InvoiceSeriesEntity? entity = await _context.InvoiceSeries.FirstOrDefaultAsync(item => item.Id == id);
        if (entity is null)
        {
            throw new AppException("Serie de comprobante no encontrada.", 404);
        }

        return entity;
    }
}
