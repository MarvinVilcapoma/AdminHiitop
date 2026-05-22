using AdminHiitop.Api.Application.DTOs.SaleImports;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.SaleImports;

public sealed class SaleImportService : ISaleImportService
{
    private readonly AdminHiitopDbContext _context;

    public SaleImportService(AdminHiitopDbContext context) => _context = context;

    public async Task<IEnumerable<SaleImport>> GetAsync()
        => await _context.SaleImports.AsNoTracking().OrderByDescending(item => item.ImportedAt).ToListAsync();

    public async Task<object> GetSummaryAsync()
        => new
        {
            total_batches = await _context.SaleImports.CountAsync(),
            total_rows = await _context.SaleImports.SumAsync(item => item.ImportedRows)
        };

    public async Task<IEnumerable<SaleImport>> GetByBatchAsync(string batch)
        => await _context.SaleImports.AsNoTracking().Where(item => item.BatchCode == batch).ToListAsync();

    public async Task<object> ImportAsync(ImportRowsRequest request)
    {
        string batch = $"BATCH-{PeruClock.Now:yyyyMMddHHmmss}";
        _context.SaleImports.Add(new SaleImport
        {
            BatchCode = batch,
            SourceFileName = "frontend-import",
            ImportedRows = request.Rows?.Count ?? 0,
            ImportedByUserId = 1,
            ImportedAt = PeruClock.Now
        });
        await _context.SaveChangesAsync();
        return new { success = true, import_batch = batch, imported_rows = request.Rows?.Count ?? 0 };
    }

    public async Task DeleteBatchAsync(string batch)
    {
        var items = await _context.SaleImports.Where(item => item.BatchCode == batch).ToListAsync();
        _context.SaleImports.RemoveRange(items);
        await _context.SaveChangesAsync();
    }
}
