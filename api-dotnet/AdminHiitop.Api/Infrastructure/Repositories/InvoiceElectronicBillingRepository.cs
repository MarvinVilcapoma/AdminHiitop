using AdminHiitop.Api.Application.Interfaces.Repositories;
using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Infrastructure.Repositories;

public sealed class InvoiceElectronicBillingRepository : IInvoiceElectronicBillingRepository
{
    private readonly AdminHiitopDbContext _context;

    public InvoiceElectronicBillingRepository(AdminHiitopDbContext context)
    {
        _context = context;
    }

    public Task<Invoice?> GetInvoiceForSendAsync(int invoiceId)
    {
        return _context.Invoices
            .Include(item => item.Order)
            .ThenInclude(item => item!.Items)
            .ThenInclude(item => item.Product)
            .Include(item => item.Order)
            .ThenInclude(item => item!.Customer)
            .Include(item => item.Order)
            .ThenInclude(item => item!.DocumentType)
            .FirstOrDefaultAsync(item => item.Id == invoiceId);
    }

    public Task<Invoice?> GetByIdAsync(int invoiceId)
    {
        return _context.Invoices.FirstOrDefaultAsync(item => item.Id == invoiceId);
    }

    public Task AddSendLogAsync(SunatSendLog sendLog)
    {
        return _context.SunatSendLogs.AddAsync(sendLog).AsTask();
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}
