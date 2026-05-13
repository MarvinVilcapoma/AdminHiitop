using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Repositories;

public interface IInvoiceElectronicBillingRepository
{
    Task<Invoice?> GetInvoiceForSendAsync(int invoiceId);
    Task<Invoice?> GetByIdAsync(int invoiceId);
    Task AddSendLogAsync(SunatSendLog sendLog);
    Task SaveChangesAsync();
}
