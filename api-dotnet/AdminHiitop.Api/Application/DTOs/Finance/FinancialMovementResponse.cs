namespace AdminHiitop.Api.Application.DTOs.Finance;

public sealed class FinancialMovementResponse
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryColor { get; set; }
    public string? CategoryIcon { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime MovementDate { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public string? SourceType { get; set; }
    public int? SourceId { get; set; }
    public bool IsFixedGenerated { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
