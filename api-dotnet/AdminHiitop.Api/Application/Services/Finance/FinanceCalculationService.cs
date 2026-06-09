using AdminHiitop.Api.Application.Interfaces.Services;

namespace AdminHiitop.Api.Application.Services.Finance;

/// <summary>
/// Central home for all financial formulas.
/// No business logic lives here — only pure calculations so they can be
/// tested in isolation and reused without side effects.
/// </summary>
public sealed class FinanceCalculationService : IFinanceCalculationService
{
    public decimal GrossProfit(decimal totalIncome, decimal totalProductCost)
        => Round(totalIncome - totalProductCost);

    public decimal NetProfit(decimal grossProfit, decimal totalExpenses)
        => Round(grossProfit - totalExpenses);

    public decimal GrossMarginPct(decimal grossProfit, decimal totalIncome)
        => totalIncome == 0 ? 0m : Round(grossProfit / totalIncome * 100);

    public decimal NetMarginPct(decimal netProfit, decimal totalIncome)
        => totalIncome == 0 ? 0m : Round(netProfit / totalIncome * 100);

    /// <summary>
    /// Gross profit for a single order line item.
    /// Formula: ((unitSalePrice × quantity) - discountAmount) - (unitCost × quantity)
    /// </summary>
    public decimal ItemGrossProfit(decimal unitSalePrice, decimal unitCost, int quantity, decimal discountAmount = 0)
    {
        decimal saleTotal = unitSalePrice * quantity - discountAmount;
        decimal costTotal = unitCost * quantity;
        return Round(saleTotal - costTotal);
    }

    public decimal InvestmentRecoveryPct(decimal netProfitAccumulated, decimal totalInvestment)
        => totalInvestment == 0 ? 0m : Round(netProfitAccumulated / totalInvestment * 100);

    public decimal PendingInvestmentRecovery(decimal totalInvestment, decimal netProfitAccumulated)
        => Round(Math.Max(0, totalInvestment - netProfitAccumulated));

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
