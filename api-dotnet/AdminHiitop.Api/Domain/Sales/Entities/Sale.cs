using AdminHiitop.Api.Domain.Common;

namespace AdminHiitop.Api.Domain.Sales.Entities;

public sealed class Sale : AuditableEntity
{
    public string? MovementType { get; set; }
    public string? DocumentTypeLabel { get; set; }
    public string? DocumentNumber { get; set; }
    public DateTime? IssueDate { get; set; }
    public string? SeriesNumber { get; set; }
    public string? SeriesPrefix { get; set; }
    public string? TrackingNumber { get; set; }
    public DateTime? SaleDateTime { get; set; }
    public string? Branch { get; set; }
    public string? Seller { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerTaxId { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerAddress { get; set; }
    public string? CustomerDistrict { get; set; }
    public string? CustomerProvince { get; set; }
    public string? CustomerDepartment { get; set; }
    public string? PriceList { get; set; }
    public string? DeliveryType { get; set; }
    public string Currency { get; set; } = "PEN";
    public decimal TotalNet { get; set; }
    public decimal TotalTax { get; set; }
    public decimal TotalGross { get; set; }
    public decimal DiscountNet { get; set; }
    public decimal DiscountGross { get; set; }
    public string? ImportSource { get; set; }
    public string? ImportBatch { get; set; }
    public int? UserId { get; set; }

    public User? User { get; set; }
    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
}
