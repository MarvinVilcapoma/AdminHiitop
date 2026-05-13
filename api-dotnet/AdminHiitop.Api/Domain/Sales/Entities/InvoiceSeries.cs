using AdminHiitop.Api.Domain.Common;

namespace AdminHiitop.Api.Domain.Sales.Entities;

public sealed class InvoiceSeries : AuditableEntity
{
    public string DocType { get; set; } = string.Empty;
    public string Serie { get; set; } = string.Empty;
    public int NextNumber { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
