using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.DailySummaries;

public sealed class DailySummaryService : IDailySummaryService
{
    private readonly AdminHiitopDbContext _context;

    public DailySummaryService(AdminHiitopDbContext context) => _context = context;

    public async Task<object> GetAsync(int perPage, int page)
    {
        var query = _context.DailySummaries.AsNoTracking().Include(item => item.Items).OrderByDescending(item => item.SummaryDate);
        return await PaginationHelper.CreateAsync(query, page, perPage);
    }

    public Task<DailySummary?> GetByIdAsync(int id)
        => _context.DailySummaries.AsNoTracking().Include(item => item.Items).FirstOrDefaultAsync(item => item.Id == id);

    public async Task<DailySummary> SendAsync(DateTime? date)
    {
        DateTime summaryDate = date ?? PeruClock.Now.Date;
        var summary = new DailySummary
        {
            SummaryDate = summaryDate,
            SummaryNumber = $"RC-{summaryDate:yyyyMMdd}-{PeruClock.Now:HHmmss}",
            Status = "ticket_generated",
            Ticket = Guid.NewGuid().ToString("N")[..12],
            SunatDescription = "Resumen diario generado."
        };
        _context.DailySummaries.Add(summary);
        await _context.SaveChangesAsync();
        return summary;
    }

    public async Task<DailySummary> CheckTicketAsync(int id)
    {
        DailySummary? entity = await _context.DailySummaries.FirstOrDefaultAsync(item => item.Id == id);
        if (entity is null) throw new AppException("Resumen diario no encontrado.", 404);
        entity.Status = "accepted";
        entity.AcceptedAt = PeruClock.Now;
        entity.SunatDescription = "Resumen diario aceptado.";
        await _context.SaveChangesAsync();
        return entity;
    }
}
