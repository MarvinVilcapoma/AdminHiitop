namespace AdminHiitop.Api.Application.DTOs.Finance;

/// <summary>
/// Enhanced financial dashboard response that includes gross profit metrics,
/// margin percentages, and investment recovery — in addition to the basic
/// income/expense summary already provided by FinancialDashboardResponse.
/// </summary>
public sealed class FinanceDashboardDto
{
    public int Year  { get; set; }
    public int Month { get; set; }

    // ── Revenue & cost ────────────────────────────────────────────────────────
    public decimal TotalIncome       { get; set; }
    public decimal TotalProductCost  { get; set; }
    public decimal GrossProfit       { get; set; }
    public decimal TotalExpenses     { get; set; }
    public decimal NetProfit         { get; set; }

    // ── Margin percentages ───────────────────────────────────────────────────
    public decimal GrossMarginPct    { get; set; }
    public decimal NetMarginPct      { get; set; }

    // ── Month-over-month comparison ──────────────────────────────────────────
    public decimal PrevMonthIncome   { get; set; }
    public decimal PrevMonthExpense  { get; set; }
    public decimal PrevMonthNet      { get; set; }

    // ── Investment recovery ───────────────────────────────────────────────────
    public decimal TotalInvestment               { get; set; }
    public decimal RecoveredInvestment           { get; set; }
    public decimal PendingInvestmentRecovery     { get; set; }
    public decimal InvestmentRecoveryPct         { get; set; }

    // ── Operational counters ─────────────────────────────────────────────────
    public int AutomaticMovementsCount   { get; set; }
    public int PendingCostOrdersCount    { get; set; }

    // ── Charts / detail ──────────────────────────────────────────────────────
    public List<MonthlySummaryItem>        MonthlySeries      { get; set; } = [];
    public List<CategorySummaryItem>       ExpensesByCategory { get; set; } = [];
    public List<ProfitByProductDto>        TopProfitProducts  { get; set; } = [];
}

public sealed class ProfitByProductDto
{
    public int?    ProductId        { get; set; }
    public string  ProductName      { get; set; } = string.Empty;
    public int     QuantitySold     { get; set; }
    public decimal TotalSaleAmount  { get; set; }
    public decimal TotalCostAmount  { get; set; }
    public decimal GrossProfitAmount { get; set; }
    public decimal MarginPct        { get; set; }
}

public sealed class SyncOrdersResponse
{
    public int TotalOrdersProcessed { get; set; }
    public int MovementsCreated     { get; set; }
    public int MovementsUpdated     { get; set; }
    public int SkippedOrders        { get; set; }
    public int PendingCostItems     { get; set; }
    public List<string> Errors      { get; set; } = [];
}

public sealed class PendingCostOrderDto
{
    public int    OrderId        { get; set; }
    public string OrderNumber    { get; set; } = string.Empty;
    public string OrderDate      { get; set; } = string.Empty;
    public string CustomerName   { get; set; } = string.Empty;
    public decimal Total         { get; set; }
    public List<PendingCostItemDto> Items { get; set; } = [];
}

public sealed class PendingCostItemDto
{
    public int    OrderItemId   { get; set; }
    public int?   ProductId     { get; set; }
    public string ProductName   { get; set; } = string.Empty;
    public int    Quantity      { get; set; }
    public decimal UnitPrice    { get; set; }
    public decimal Subtotal     { get; set; }
}
