using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Sales;

public sealed class SaleService : ISaleService
{
    private readonly AdminHiitopDbContext _context;

    public SaleService(AdminHiitopDbContext context) => _context = context;

    public async Task<object> GetAsync(int perPage, int page, CancellationToken cancellationToken)
        => await PaginationHelper.CreateAsync(
            _context.Sales.AsNoTracking().Include(item => item.Items).OrderByDescending(item => item.SaleDateTime).ThenByDescending(item => item.Id),
            page, perPage, cancellationToken);

    public IEnumerable<string> GetBranches() => ["Tienda Principal", "Sucursal 1"];

    public Task<Sale?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => _context.Sales.AsNoTracking().Include(item => item.Items).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<Sale> CreateAsync(Sale request, CancellationToken cancellationToken)
    {
        _context.Sales.Add(request);
        await _context.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<Sale> UpdateAsync(int id, Sale request, CancellationToken cancellationToken)
    {
        Sale entity = await FindAsync(id, cancellationToken);
        entity.DocumentTypeLabel = request.DocumentTypeLabel ?? entity.DocumentTypeLabel;
        entity.SeriesNumber = request.SeriesNumber ?? entity.SeriesNumber;
        entity.SaleDateTime = request.SaleDateTime;
        entity.Branch = request.Branch ?? entity.Branch;
        entity.Seller = request.Seller ?? entity.Seller;
        entity.CustomerName = request.CustomerName ?? entity.CustomerName;
        entity.CustomerTaxId = request.CustomerTaxId ?? entity.CustomerTaxId;
        entity.Currency = request.Currency ?? entity.Currency;
        entity.TotalGross = request.TotalGross;
        entity.TotalNet = request.TotalNet;
        entity.TotalTax = request.TotalTax;
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        Sale entity = await FindAsync(id, cancellationToken);
        _context.Sales.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Sale> FindAsync(int id, CancellationToken cancellationToken)
    {
        Sale? entity = await _context.Sales.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) throw new AppException("Venta no encontrada.", 404);
        return entity;
    }
}
