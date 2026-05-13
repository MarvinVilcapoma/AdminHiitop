using AdminHiitop.Api.Domain.Common;

namespace AdminHiitop.Api.Domain.Catalog.Entities;

public sealed class Size : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ProductType> ProductTypes { get; set; } = new List<ProductType>();
}
