namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IFinanceCalculationService
{
    decimal GrossProfit(decimal totalIncome, decimal totalProductCost);
    decimal NetProfit(decimal grossProfit, decimal totalExpenses);
    decimal GrossMarginPct(decimal grossProfit, decimal totalIncome);
    decimal NetMarginPct(decimal netProfit, decimal totalIncome);
    decimal ItemGrossProfit(decimal unitSalePrice, decimal unitCost, int quantity, decimal discountAmount = 0);
    decimal InvestmentRecoveryPct(decimal netProfitAccumulated, decimal totalInvestment);
    decimal PendingInvestmentRecovery(decimal totalInvestment, decimal netProfitAccumulated);
}
