namespace AdminHiitop.Api.Application.DTOs.Finance;

public sealed class FinancialDashboardResponse
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal NetProfit { get; set; }
    public decimal PrevMonthIncome { get; set; }
    public decimal PrevMonthExpense { get; set; }
    public decimal PrevMonthNetProfit { get; set; }
    public List<MonthlySummaryItem> MonthlySeries { get; set; } = [];
    public List<CategorySummaryItem> ExpensesByCategory { get; set; } = [];
    public List<CategorySummaryItem> IncomesByCategory { get; set; } = [];
    public List<FinancialMovementResponse> RecentMovements { get; set; } = [];
}

public sealed class MonthlySummaryItem
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
    public decimal Net { get; set; }
}

public sealed class CategorySummaryItem
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? CategoryColor { get; set; }
    public string? CategoryIcon { get; set; }
    public decimal Total { get; set; }
    public int Count { get; set; }
}
