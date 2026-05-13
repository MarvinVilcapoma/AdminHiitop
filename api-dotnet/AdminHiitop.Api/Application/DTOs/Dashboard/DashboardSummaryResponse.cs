namespace AdminHiitop.Api.Application.DTOs.Dashboard;

public sealed class DashboardSummaryResponse
{
    public int TotalProducts { get; init; }
    public int ActiveProducts { get; init; }
    public int TotalCustomers { get; init; }
    public int ActiveCustomers { get; init; }
    public int TotalOrders { get; init; }
    public int PendingOrders { get; init; }
    public int TotalInvoices { get; init; }
    public int PendingInvoices { get; init; }
}
