using AdminHiitop.Api.Application.DTOs.SaleImports;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.SaleImports;

public sealed class SaleImportService : ISaleImportService
{
    private readonly AdminHiitopDbContext _context;

    public SaleImportService(AdminHiitopDbContext context) => _context = context;

    public async Task<IEnumerable<SaleImport>> GetAsync(CancellationToken cancellationToken)
        => await _context.SaleImports.AsNoTracking().OrderByDescending(item => item.ImportedAt).ToListAsync(cancellationToken);

    public async Task<object> GetSummaryAsync(CancellationToken cancellationToken)
        => new
        {
            total_batches = await _context.SaleImports.CountAsync(cancellationToken),
            total_rows = await _context.SaleImports.SumAsync(item => item.ImportedRows, cancellationToken)
        };

    public async Task<IEnumerable<SaleImport>> GetByBatchAsync(string batch, CancellationToken cancellationToken)
        => await _context.SaleImports.AsNoTracking().Where(item => item.BatchCode == batch).ToListAsync(cancellationToken);

    public async Task<object> ImportAsync(ImportRowsRequest request, CancellationToken cancellationToken)
    {
        string batch = $"BATCH-{DateTime.UtcNow:yyyyMMddHHmmss}";
        _context.SaleImports.Add(new SaleImport
        {
            BatchCode = batch,
            SourceFileName = "frontend-import",
            ImportedRows = request.Rows?.Count ?? 0,
            ImportedByUserId = 1,
            ImportedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);
        return new { success = true, import_batch = batch, imported_rows = request.Rows?.Count ?? 0 };
    }

    public async Task DeleteBatchAsync(string batch, CancellationToken cancellationToken)
    {
        var items = await _context.SaleImports.Where(item => item.BatchCode == batch).ToListAsync(cancellationToken);
        _context.SaleImports.RemoveRange(items);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
