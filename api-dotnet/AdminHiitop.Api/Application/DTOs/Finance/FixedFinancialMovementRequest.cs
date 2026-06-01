namespace AdminHiitop.Api.Application.DTOs.Finance;

public sealed class FixedFinancialMovementRequest
{
    public string Type { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Frequency { get; set; } = "MONTHLY";
    public int? DayOfMonth { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? PaymentMethod { get; set; }
    public bool AutoGenerate { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}
