using AdminHiitop.Api.Domain.Common;
using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Domain.Catalog.Entities;

public sealed class Customer : AuditableEntity
{
    public string FullName { get; set; } = string.Empty;
    public string? Dni { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public int? ProvinceId { get; set; }
    public int? DistrictId { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public string? DocumentType { get; set; }
    public string? Ruc { get; set; }
    public string? RazonSocial { get; set; }
    public string? NombreComercial { get; set; }

    public Province? Province { get; set; }
    public District? District { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
