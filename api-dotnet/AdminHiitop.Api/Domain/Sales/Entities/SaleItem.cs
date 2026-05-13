using AdminHiitop.Api.Domain.Common;

namespace AdminHiitop.Api.Domain.Sales.Entities;

public sealed class SaleItem : AuditableEntity
{
    public int SaleId { get; set; }
    public string? ProductType { get; set; }
    public string? Sku { get; set; }
    public string? ProductName { get; set; }
    public string? Variant { get; set; }
    public string? OtherAttributes { get; set; }
    public string? Brand { get; set; }
    public string? PackDetail { get; set; }
    public decimal ListPrice { get; set; }
    public decimal UnitNetPrice { get; set; }
    public decimal UnitGrossPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal TotalNet { get; set; }
    public decimal TotalTax { get; set; }
    public decimal TotalGross { get; set; }
    public string? DiscountName { get; set; }
    public decimal DiscountNet { get; set; }
    public decimal DiscountGross { get; set; }
    public decimal DiscountPct { get; set; }
    public decimal UnitCostNet { get; set; }
    public decimal TotalCostNet { get; set; }
    public decimal Margin { get; set; }
    public decimal MarginPct { get; set; }

    public Sale Sale { get; set; } = null!;
}
