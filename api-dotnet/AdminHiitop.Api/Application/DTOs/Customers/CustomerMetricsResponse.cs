namespace AdminHiitop.Api.Application.DTOs.Customers;

public sealed class CustomerMetricsResponse
{
    public int    CustomerId  { get; set; }
    public string FullName    { get; set; } = string.Empty;
    public string? Phone      { get; set; }
    public string? Email      { get; set; }
    public int    OrderCount  { get; set; }
    public decimal TotalSpent { get; set; }
    public List<SizeMetric> TopSizes { get; set; } = [];
}

public sealed class SizeMetric
{
    public string Size     { get; set; } = string.Empty;
    public int    Quantity { get; set; }
}
