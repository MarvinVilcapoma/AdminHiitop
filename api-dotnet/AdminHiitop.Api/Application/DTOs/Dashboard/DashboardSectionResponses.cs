namespace AdminHiitop.Api.Application.DTOs.Dashboard;

public sealed class DashboardProductsResponse
{
    public int TotalProducts { get; init; }
    public int ActiveProducts { get; init; }
    public int InactiveProducts => Math.Max(0, TotalProducts - ActiveProducts);
}

public sealed class DashboardCustomersResponse
{
    public int TotalCustomers { get; init; }
    public int ActiveCustomers { get; init; }
    public int NewCustomers { get; init; }
}

public sealed class DashboardOrdersResponse
{
    public int TotalOrders { get; init; }
    public int PendingOrders { get; init; }
    public int DeliveredOrders { get; init; }
}

public sealed class DashboardInvoicesResponse
{
    public int TotalInvoices { get; init; }
    public int PendingInvoices { get; init; }
    public int AcceptedInvoices { get; init; }
}

public sealed class DashboardAnalyticsSummaryResponse
{
    public int TotalOrders { get; init; }
    public decimal TotalRevenue { get; init; }
    public decimal AvgTicket { get; init; }
    public int TotalUnits { get; init; }
    public decimal TotalCost { get; init; }
    public decimal TotalProfit { get; init; }
    public decimal? AvgMarginPct { get; init; }
    public int PosSalesCount { get; init; }
    public decimal PosSalesRevenue { get; init; }
    public int PendingOrders { get; init; }
    public int NewCustomers { get; init; }
}

public sealed class DashboardSalesByDayResponse
{
    public string Date { get; init; } = string.Empty;
    public int Orders { get; init; }
    public decimal Revenue { get; init; }
}

public sealed class DashboardTopProductResponse
{
    public string ProductDescription { get; init; } = string.Empty;
    public int TotalQty { get; init; }
    public decimal TotalRevenue { get; init; }
}

public sealed class DashboardBranchResponse
{
    public string Branch { get; init; } = string.Empty;
    public int TotalOrders { get; init; }
    public decimal TotalRevenue { get; init; }
}

public sealed class DashboardPaymentMethodBreakdownResponse
{
    public string Method { get; init; } = string.Empty;
    public decimal Total { get; init; }
}

public sealed class DashboardSellerResponse
{
    public string Seller { get; init; } = string.Empty;
    public int TotalOrders { get; init; }
    public decimal TotalRevenue { get; init; }
    public decimal AvgTicket { get; init; }
}

public sealed class DashboardMonthBranchRow
{
    public string Branch { get; init; } = string.Empty;
    public int Orders { get; init; }
    public decimal Revenue { get; init; }
}

public sealed class DashboardSalesByMonthResponse
{
    public string Month { get; init; } = string.Empty;       // "2026-01"
    public string MonthLabel { get; init; } = string.Empty;  // "Ene 2026"
    public int Orders { get; init; }
    public decimal Revenue { get; init; }
    public List<DashboardMonthBranchRow> Branches { get; init; } = [];
}
