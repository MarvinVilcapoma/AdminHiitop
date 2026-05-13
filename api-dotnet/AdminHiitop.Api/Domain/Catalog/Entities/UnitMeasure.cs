using System.ComponentModel.DataAnnotations.Schema;
using AdminHiitop.Api.Domain.Common;
using AdminHiitop.Api.Domain.Inventory.Entities;

namespace AdminHiitop.Api.Domain.Catalog.Entities;

public sealed class UnitMeasure : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? SunatCode { get; set; }
    public bool IsActive { get; set; } = true;

    [NotMapped]
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
