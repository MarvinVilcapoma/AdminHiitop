using AdminHiitop.Api.Application.DTOs.Finance;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Finance;

/// <summary>
/// Enhanced dashboard service that adds gross profit, net profit, margins,
/// and investment recovery on top of the existing income/expense data.
/// The original FinancialDashboardService is kept intact for backward compatibility.
/// </summary>
public sealed class FinanceDashboardService : IFinanceDashboardService
{
    private readonly AdminHiitopDbContext      _context;
    private readonly IFinanceCalculationService _calc;

    public FinanceDashboardService(AdminHiitopDbContext context, IFinanceCalculationService calc)
    {
        _context = context;
        _calc    = calc;
    }

    public async Task<FinanceDashboardDto> GetDashboardAsync(int year, int month)
    {
        // ── Current period movements ─────────────────────────────────────────
        var movements = await _context.FinancialMovements
            .Include(m => m.Category)
            .Where(m => m.DeletedAt == null
                     && m.MovementDate.Year  == year
                     && m.MovementDate.Month == month)
            .ToListAsync();

        decimal totalIncome      = movements.Where(m => m.Type == "INCOME").Sum(m => m.Amount);
        decimal totalProductCost = movements.Where(m => m.Type == "INCOME").Sum(m => m.CostAmount);
        decimal totalExpenses    = movements.Where(m => m.Type == "EXPENSE").Sum(m => m.Amount);
        decimal grossProfit      = _calc.GrossProfit(totalIncome, totalProductCost);
        decimal netProfit        = _calc.NetProfit(grossProfit, totalExpenses);

        // ── Previous period for comparison ───────────────────────────────────
        int prevYear  = month == 1 ? year - 1 : year;
        int prevMonth = month == 1 ? 12        : month - 1;
        var prevMovements = await _context.FinancialMovements
            .Where(m => m.DeletedAt == null
                     && m.MovementDate.Year  == prevYear
                     && m.MovementDate.Month == prevMonth)
            .ToListAsync();
        decimal prevIncome  = prevMovements.Where(m => m.Type == "INCOME").Sum(m => m.Amount);
        decimal prevExpense = prevMovements.Where(m => m.Type == "EXPENSE").Sum(m => m.Amount);

        // ── Investments (all-time, not filtered by period) ───────────────────
        decimal totalInvestment = await _context.Investments
            .Where(i => i.IsActive && i.DeletedAt == null)
            .SumAsync(i => i.Amount);

        // Net profit accumulated all-time for ROI
        decimal allTimeNetProfit = await ComputeAllTimeNetProfitAsync();
        decimal recoveredPct     = _calc.InvestmentRecoveryPct(allTimeNetProfit, totalInvestment);
        decimal pendingRecovery  = _calc.PendingInvestmentRecovery(totalInvestment, allTimeNetProfit);

        // ── Operational counters ─────────────────────────────────────────────
        int automaticCount = movements.Count(m => m.IsAutomatic);
        int pendingCostCount = await _context.FinancialMovementItems
            .Where(i => i.IsCostPending && i.DeletedAt == null)
            .Select(i => i.FinancialMovementId)
            .Distinct()
            .CountAsync();

        // ── Charts ───────────────────────────────────────────────────────────
        var monthlySeries    = await GetMonthlySeries(year);
        var expensesByCategory = BuildCategorySummary(movements.Where(m => m.Type == "EXPENSE"));
        var topProducts      = await GetTopProfitProductsAsync(year, month, 10);

        return new FinanceDashboardDto
        {
            Year                      = year,
            Month                     = month,
            TotalIncome               = totalIncome,
            TotalProductCost          = totalProductCost,
            GrossProfit               = grossProfit,
            TotalExpenses             = totalExpenses,
            NetProfit                 = netProfit,
            GrossMarginPct            = _calc.GrossMarginPct(grossProfit, totalIncome),
            NetMarginPct              = _calc.NetMarginPct(netProfit, totalIncome),
            PrevMonthIncome           = prevIncome,
            PrevMonthExpense          = prevExpense,
            PrevMonthNet              = prevIncome - prevExpense,
            TotalInvestment           = totalInvestment,
            RecoveredInvestment       = Math.Min(allTimeNetProfit, totalInvestment),
            PendingInvestmentRecovery = pendingRecovery,
            InvestmentRecoveryPct     = recoveredPct,
            AutomaticMovementsCount   = automaticCount,
            PendingCostOrdersCount    = pendingCostCount,
            MonthlySeries             = monthlySeries,
            ExpensesByCategory        = expensesByCategory,
            TopProfitProducts         = topProducts,
        };
    }

    public async Task<List<PendingCostOrderDto>> GetPendingCostOrdersAsync()
    {
        // Find orders that have been synced but have items without cost
        var pendingItemMovementIds = await _context.FinancialMovementItems
            .Where(i => i.IsCostPending && i.DeletedAt == null)
            .Select(i => i.FinancialMovementId)
            .Distinct()
            .ToListAsync();

        if (!pendingItemMovementIds.Any()) return [];

        var movements = await _context.FinancialMovements
            .Where(m => pendingItemMovementIds.Contains(m.Id) && m.SourceType == "ORDER" && m.DeletedAt == null)
            .ToListAsync();

        var orderIds = movements.Select(m => m.SourceId).Where(id => id.HasValue).Select(id => id!.Value).ToList();
        var orders   = await _context.Orders
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Where(o => orderIds.Contains(o.Id))
            .ToListAsync();

        return orders.Select(o => new PendingCostOrderDto
        {
            OrderId      = o.Id,
            OrderNumber  = o.OrderNumber,
            OrderDate    = o.OrderDate.ToString("yyyy-MM-dd"),
            CustomerName = o.CustomerName ?? o.Customer?.FullName ?? "—",
            Total        = o.Total,
            Items        = o.Items
                .Where(i => i.Product is null || i.Product.UnitCost == 0)
                .Select(i => new PendingCostItemDto
                {
                    OrderItemId = i.Id,
                    ProductId   = i.ProductId,
                    ProductName = i.Product?.Name ?? i.ProductDescription ?? "—",
                    Quantity    = i.Quantity,
                    UnitPrice   = i.UnitPrice,
                    Subtotal    = i.Subtotal,
                })
                .ToList(),
        })
        .Where(dto => dto.Items.Any())
        .ToList();
    }

    public async Task<List<ProfitByProductDto>> GetProfitByProductAsync(DateTime from, DateTime to)
    {
        var items = await _context.FinancialMovementItems
            .Include(i => i.FinancialMovement)
            .Where(i =>
                i.DeletedAt == null &&
                !i.IsCostPending   &&
                i.FinancialMovement != null &&
                i.FinancialMovement.DeletedAt   == null &&
                i.FinancialMovement.MovementDate >= from  &&
                i.FinancialMovement.MovementDate <= to)
            .ToListAsync();

        return items
            .GroupBy(i => new { i.ProductId, i.ProductName })
            .Select(g => new ProfitByProductDto
            {
                ProductId         = g.Key.ProductId,
                ProductName       = g.Key.ProductName,
                QuantitySold      = g.Sum(i => i.Quantity),
                TotalSaleAmount   = g.Sum(i => i.TotalSaleAmount),
                TotalCostAmount   = g.Sum(i => i.TotalCostAmount),
                GrossProfitAmount = g.Sum(i => i.GrossProfitAmount),
                MarginPct         = g.Sum(i => i.TotalSaleAmount) == 0 ? 0
                    : Math.Round(g.Sum(i => i.GrossProfitAmount) / g.Sum(i => i.TotalSaleAmount) * 100, 1),
            })
            .OrderByDescending(p => p.GrossProfitAmount)
            .ToList();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<decimal> ComputeAllTimeNetProfitAsync()
    {
        decimal income  = await _context.FinancialMovements.Where(m => m.Type == "INCOME"  && m.DeletedAt == null).SumAsync(m => m.Amount);
        decimal expense = await _context.FinancialMovements.Where(m => m.Type == "EXPENSE" && m.DeletedAt == null).SumAsync(m => m.Amount);
        decimal cost    = await _context.FinancialMovements.Where(m => m.Type == "INCOME"  && m.DeletedAt == null).SumAsync(m => m.CostAmount);
        return (income - cost) - expense;
    }

    private async Task<List<MonthlySummaryItem>> GetMonthlySeries(int year)
    {
        var data = await _context.FinancialMovements
            .Where(m => m.MovementDate.Year == year && m.DeletedAt == null)
            .GroupBy(m => new { m.MovementDate.Month, m.Type })
            .Select(g => new { g.Key.Month, g.Key.Type, Total = g.Sum(m => m.Amount) })
            .ToListAsync();

        string[] labels = ["Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic"];
        return Enumerable.Range(1, 12).Select(m =>
        {
            decimal inc = data.Where(d => d.Month == m && d.Type == "INCOME").Sum(d => d.Total);
            decimal exp = data.Where(d => d.Month == m && d.Type == "EXPENSE").Sum(d => d.Total);
            return new MonthlySummaryItem { Year = year, Month = m, Label = labels[m - 1], Income = inc, Expense = exp, Net = inc - exp };
        }).ToList();
    }

    private static List<CategorySummaryItem> BuildCategorySummary(
        IEnumerable<Domain.Finance.Entities.FinancialMovement> movements)
        => movements
            .GroupBy(m => m.CategoryId)
            .Select(g => new CategorySummaryItem
            {
                CategoryId    = g.Key,
                CategoryName  = g.First().Category?.Name ?? "Sin categoría",
                CategoryColor = g.First().Category?.Color,
                CategoryIcon  = g.First().Category?.Icon,
                Total         = g.Sum(m => m.Amount),
                Count         = g.Count(),
            })
            .OrderByDescending(c => c.Total)
            .ToList();

    private async Task<List<ProfitByProductDto>> GetTopProfitProductsAsync(int year, int month, int top)
    {
        var items = await _context.FinancialMovementItems
            .Include(i => i.FinancialMovement)
            .Where(i =>
                i.DeletedAt == null &&
                !i.IsCostPending   &&
                i.FinancialMovement != null &&
                i.FinancialMovement.DeletedAt   == null &&
                i.FinancialMovement.MovementDate.Year  == year  &&
                i.FinancialMovement.MovementDate.Month == month)
            .ToListAsync();

        return items
            .GroupBy(i => new { i.ProductId, i.ProductName })
            .Select(g => new ProfitByProductDto
            {
                ProductId         = g.Key.ProductId,
                ProductName       = g.Key.ProductName,
                QuantitySold      = g.Sum(i => i.Quantity),
                TotalSaleAmount   = g.Sum(i => i.TotalSaleAmount),
                TotalCostAmount   = g.Sum(i => i.TotalCostAmount),
                GrossProfitAmount = g.Sum(i => i.GrossProfitAmount),
                MarginPct         = g.Sum(i => i.TotalSaleAmount) == 0 ? 0
                    : Math.Round(g.Sum(i => i.GrossProfitAmount) / g.Sum(i => i.TotalSaleAmount) * 100, 1),
            })
            .OrderByDescending(p => p.GrossProfitAmount)
            .Take(top)
            .ToList();
    }
}
