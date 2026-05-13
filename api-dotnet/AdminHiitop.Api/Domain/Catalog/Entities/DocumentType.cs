using AdminHiitop.Api.Domain.Common;
using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Domain.Catalog.Entities;

public sealed class DocumentType : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsProtected { get; set; }
    public bool IsSunatDocument { get; set; }
    public bool RequiresCustomer { get; set; }
    public bool RequiresRelatedDocument { get; set; }
    public bool CanBeConverted { get; set; }
    public bool IsCommercialDocument { get; set; }
    public int SortOrder { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<DocumentTypePrintFormat> DocumentTypePrintFormats { get; set; } = new List<DocumentTypePrintFormat>();
}
