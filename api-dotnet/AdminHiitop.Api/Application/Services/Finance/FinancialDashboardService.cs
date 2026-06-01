using AdminHiitop.Api.Application.DTOs.Finance;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Finance;

public sealed class FinancialDashboardService : IFinancialDashboardService
{
    private static readonly string[] MonthLabels =
        ["Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic"];

    private readonly AdminHiitopDbContext _context;

    public FinancialDashboardService(AdminHiitopDbContext context)
    {
        _context = context;
    }

    public async Task<FinancialDashboardResponse> GetMonthlySummaryAsync(int year, int month)
    {
        var movements = await _context.FinancialMovements
            .Include(m => m.Category)
            .Where(m => m.MovementDate.Year == year && m.MovementDate.Month == month)
            .ToListAsync();

        decimal totalIncome  = movements.Where(m => m.Type == "INCOME").Sum(m => m.Amount);
        decimal totalExpense = movements.Where(m => m.Type == "EXPENSE").Sum(m => m.Amount);

        // Previous month figures
        int prevYear  = month == 1 ? year - 1 : year;
        int prevMonth = month == 1 ? 12 : month - 1;

        var prevMovements = await _context.FinancialMovements
            .Where(m => m.MovementDate.Year == prevYear && m.MovementDate.Month == prevMonth)
            .ToListAsync();

        decimal prevIncome  = prevMovements.Where(m => m.Type == "INCOME").Sum(m => m.Amount);
        decimal prevExpense = prevMovements.Where(m => m.Type == "EXPENSE").Sum(m => m.Amount);

        // Category summaries
        var expensesByCategory = movements
            .Where(m => m.Type == "EXPENSE")
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

        var incomesByCategory = movements
            .Where(m => m.Type == "INCOME")
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

        // Recent movements (last 10)
        var recent = movements
            .OrderByDescending(m => m.MovementDate)
            .ThenByDescending(m => m.Id)
            .Take(10)
            .Select(m => new FinancialMovementResponse
            {
                Id            = m.Id,
                Type          = m.Type,
                CategoryId    = m.CategoryId,
                CategoryName  = m.Category?.Name,
                CategoryColor = m.Category?.Color,
                CategoryIcon  = m.Category?.Icon,
                Description   = m.Description,
                Amount        = m.Amount,
                MovementDate  = m.MovementDate,
                PaymentMethod = m.PaymentMethod,
                CreatedAt     = m.CreatedAt,
                UpdatedAt     = m.UpdatedAt,
            })
            .ToList();

        // 12-month series for chart
        var monthlySeries = await GetYearlySummaryAsync(year);

        return new FinancialDashboardResponse
        {
            Year                 = year,
            Month                = month,
            TotalIncome          = totalIncome,
            TotalExpense         = totalExpense,
            NetProfit            = totalIncome - totalExpense,
            PrevMonthIncome      = prevIncome,
            PrevMonthExpense     = prevExpense,
            PrevMonthNetProfit   = prevIncome - prevExpense,
            MonthlySeries        = monthlySeries,
            ExpensesByCategory   = expensesByCategory,
            IncomesByCategory    = incomesByCategory,
            RecentMovements      = recent,
        };
    }

    public async Task<List<MonthlySummaryItem>> GetYearlySummaryAsync(int year)
    {
        var data = await _context.FinancialMovements
            .Where(m => m.MovementDate.Year == year)
            .GroupBy(m => new { m.MovementDate.Month, m.Type })
            .Select(g => new { g.Key.Month, g.Key.Type, Total = g.Sum(m => m.Amount) })
            .ToListAsync();

        return Enumerable.Range(1, 12).Select(m =>
        {
            decimal inc = data.Where(d => d.Month == m && d.Type == "INCOME").Sum(d => d.Total);
            decimal exp = data.Where(d => d.Month == m && d.Type == "EXPENSE").Sum(d => d.Total);
            return new MonthlySummaryItem
            {
                Year    = year,
                Month   = m,
                Label   = MonthLabels[m - 1],
                Income  = inc,
                Expense = exp,
                Net     = inc - exp,
            };
        }).ToList();
    }

    public async Task<List<CategorySummaryItem>> GetCategorySummaryAsync(int year, int month, string? type = null)
    {
        IQueryable<Domain.Finance.Entities.FinancialMovement> query = _context.FinancialMovements
            .Include(m => m.Category)
            .Where(m => m.MovementDate.Year == year && m.MovementDate.Month == month);

        if (!string.IsNullOrEmpty(type))
            query = query.Where(m => m.Type == type.ToUpperInvariant());

        var movements = await query.ToListAsync();

        return movements
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
    }
}
