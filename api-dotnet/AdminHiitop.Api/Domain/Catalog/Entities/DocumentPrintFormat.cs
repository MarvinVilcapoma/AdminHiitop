using AdminHiitop.Api.Domain.Common;
using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Domain.Catalog.Entities;

public sealed class DocumentPrintFormat : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public int? WidthMm { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<DocumentTypePrintFormat> DocumentTypePrintFormats { get; set; } = new List<DocumentTypePrintFormat>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
