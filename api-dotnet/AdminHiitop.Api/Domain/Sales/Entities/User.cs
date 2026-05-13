using AdminHiitop.Api.Domain.Common;
using AdminHiitop.Api.Domain.Inventory.Entities;

namespace AdminHiitop.Api.Domain.Sales.Entities;

public sealed class User : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? EmailVerifiedAt { get; set; }
    public string? RememberToken { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}
