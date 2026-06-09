using AdminHiitop.Api.Application.DTOs.Finance;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Finance.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Finance;

/// <summary>
/// Bridges the orders module with the finance module.
/// Responsible for creating, updating, and adjusting FinancialMovements
/// that originate from orders. All operations are idempotent.
/// </summary>
public sealed class OrderFinanceIntegrationService : IOrderFinanceIntegrationService
{
    // Order status slugs that represent completed, paid, or confirmed sales.
    // Adjust this list if new statuses are added to the system.
    private static readonly HashSet<string> IncomeGeneratingStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "delivered", "entregado" };

    private static readonly HashSet<string> CancelledStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "cancelled", "cancelado" };

    private const string SourceTypeOrder = "ORDER";

    private readonly AdminHiitopDbContext     _context;
    private readonly IFinanceCalculationService _calc;

    public OrderFinanceIntegrationService(
        AdminHiitopDbContext context,
        IFinanceCalculationService calc)
    {
        _context = context;
        _calc    = calc;
    }

    public async Task<int?> GenerateFromOrderAsync(int orderId, int? userId = null)
    {
        var order = await _context.Orders
            .Include(o => o.OrderStatus)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.DeletedAt == null);

        if (order is null) return null;

        // Only confirmed/completed orders generate real income
        if (!IncomeGeneratingStatuses.Contains(order.OrderStatus.Slug))
            return null;

        // Idempotency: find existing movement for this order
        var existing = await _context.FinancialMovements
            .Include(m => m.Items)
            .FirstOrDefaultAsync(m =>
                m.SourceType == SourceTypeOrder &&
                m.SourceId   == orderId         &&
                m.DeletedAt  == null);

        var (revenue, cost, items) = CalculateOrderFinancials(order.Items);
        decimal grossProfit        = _calc.GrossProfit(revenue, cost);

        // Find or default the "VENTAS" income category
        int categoryId = await ResolveSalesCategoryIdAsync();

        if (existing is not null)
        {
            // Update amounts if the order total changed
            existing.Amount              = revenue;
            existing.CostAmount          = cost;
            existing.GrossProfitAmount   = grossProfit;
            existing.MovementDate        = order.OrderDate;
            existing.UpdatedBy           = userId;
            existing.UpdatedAt           = DateTime.UtcNow;

            // Replace items
            _context.FinancialMovementItems.RemoveRange(existing.Items);
            existing.Items = items.Select(i => { i.FinancialMovementId = existing.Id; return i; }).ToList();
            await _context.SaveChangesAsync();
            return existing.Id;
        }

        // Create new movement
        var movement = new FinancialMovement
        {
            Type               = "INCOME",
            CategoryId         = categoryId,
            Description        = $"Pedido #{order.OrderNumber}",
            Amount             = revenue,
            CostAmount         = cost,
            GrossProfitAmount  = grossProfit,
            MovementDate       = order.OrderDate,
            SourceType         = SourceTypeOrder,
            SourceId           = orderId,
            IsAutomatic        = true,
            IsFixedGenerated   = false,
            CreatedBy          = userId,
            CreatedAt          = DateTime.UtcNow,
            UpdatedAt          = DateTime.UtcNow,
        };

        _context.FinancialMovements.Add(movement);
        await _context.SaveChangesAsync();

        // Now that movement.Id is available, attach items
        foreach (var item in items)
            item.FinancialMovementId = movement.Id;

        _context.FinancialMovementItems.AddRange(items);
        await _context.SaveChangesAsync();

        return movement.Id;
    }

    public async Task CreateAdjustmentAsync(int originalMovementId, string reason, int? userId = null)
    {
        var original = await _context.FinancialMovements
            .FirstOrDefaultAsync(m => m.Id == originalMovementId && m.DeletedAt == null);

        if (original is null) return;

        // Don't create duplicate adjustments
        bool alreadyAdjusted = await _context.FinancialMovements
            .AnyAsync(m => m.ParentMovementId == originalMovementId && m.DeletedAt == null);
        if (alreadyAdjusted) return;

        var adjustment = new FinancialMovement
        {
            Type               = original.Type,
            CategoryId         = original.CategoryId,
            Description        = $"Ajuste: {reason}",
            Amount             = -original.Amount,
            CostAmount         = -original.CostAmount,
            GrossProfitAmount  = -original.GrossProfitAmount,
            MovementDate       = DateTime.UtcNow,
            SourceType         = "ADJUSTMENT",
            SourceId           = original.SourceId,
            ParentMovementId   = originalMovementId,
            IsAutomatic        = true,
            CreatedBy          = userId,
            CreatedAt          = DateTime.UtcNow,
            UpdatedAt          = DateTime.UtcNow,
        };

        _context.FinancialMovements.Add(adjustment);
        await _context.SaveChangesAsync();
    }

    public async Task<SyncOrdersResponse> SyncAllOrdersAsync(int? userId = null)
    {
        var response = new SyncOrdersResponse();

        // Load all income-generating orders
        var orders = await _context.Orders
            .Include(o => o.OrderStatus)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Where(o => o.DeletedAt == null)
            .ToListAsync();

        // Existing movements indexed by source_id for fast lookup
        var existingSourceIds = (await _context.FinancialMovements
            .Where(m => m.SourceType == SourceTypeOrder && m.DeletedAt == null)
            .Select(m => m.SourceId)
            .ToListAsync())
            .ToHashSet();

        int salesCategoryId = await ResolveSalesCategoryIdAsync();

        foreach (var order in orders)
        {
            response.TotalOrdersProcessed++;
            try
            {
                if (!IncomeGeneratingStatuses.Contains(order.OrderStatus.Slug))
                {
                    response.SkippedOrders++;
                    continue;
                }

                if (existingSourceIds.Contains(order.Id))
                {
                    response.MovementsUpdated++;
                    continue; // already synced
                }

                var (revenue, cost, items) = CalculateOrderFinancials(order.Items);
                decimal grossProfit = _calc.GrossProfit(revenue, cost);

                var movement = new FinancialMovement
                {
                    Type               = "INCOME",
                    CategoryId         = salesCategoryId,
                    Description        = $"Pedido #{order.OrderNumber}",
                    Amount             = revenue,
                    CostAmount         = cost,
                    GrossProfitAmount  = grossProfit,
                    MovementDate       = order.OrderDate,
                    SourceType         = SourceTypeOrder,
                    SourceId           = order.Id,
                    IsAutomatic        = true,
                    CreatedBy          = userId,
                    CreatedAt          = DateTime.UtcNow,
                    UpdatedAt          = DateTime.UtcNow,
                };

                _context.FinancialMovements.Add(movement);
                await _context.SaveChangesAsync();

                int pendingItems = items.Count(i => i.IsCostPending);
                response.PendingCostItems += pendingItems;

                foreach (var item in items)
                    item.FinancialMovementId = movement.Id;

                _context.FinancialMovementItems.AddRange(items);
                await _context.SaveChangesAsync();

                response.MovementsCreated++;
            }
            catch (Exception ex)
            {
                response.Errors.Add($"Pedido {order.OrderNumber}: {ex.Message}");
            }
        }

        return response;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private (decimal revenue, decimal cost, List<FinancialMovementItem> items)
        CalculateOrderFinancials(IEnumerable<Domain.Sales.Entities.OrderItem> orderItems)
    {
        decimal totalRevenue = 0;
        decimal totalCost    = 0;
        var items = new List<FinancialMovementItem>();

        foreach (var oi in orderItems)
        {
            decimal saleTotal    = oi.Subtotal;
            decimal unitCost     = oi.Product?.UnitCost ?? 0;
            bool    costPending  = oi.Product is null || oi.Product.UnitCost == 0;
            decimal costTotal    = unitCost * oi.Quantity;
            decimal grossProfit  = _calc.ItemGrossProfit(oi.UnitPrice, unitCost, oi.Quantity);

            totalRevenue += saleTotal;
            totalCost    += costTotal;

            items.Add(new FinancialMovementItem
            {
                ProductId          = oi.ProductId,
                ProductCode        = oi.ProductKey,
                ProductName        = oi.Product?.Name ?? oi.ProductDescription ?? "—",
                Quantity           = oi.Quantity,
                UnitSalePrice      = oi.UnitPrice,
                UnitCostSnapshot   = unitCost,
                TotalSaleAmount    = saleTotal,
                TotalCostAmount    = costTotal,
                GrossProfitAmount  = grossProfit,
                IsCostPending      = costPending,
                CreatedAt          = DateTime.UtcNow,
                UpdatedAt          = DateTime.UtcNow,
            });
        }

        return (totalRevenue, totalCost, items);
    }

    private async Task<int> ResolveSalesCategoryIdAsync()
    {
        var cat = await _context.FinancialCategories
            .Where(c => c.Code == "VENTAS" && c.DeletedAt == null)
            .Select(c => c.Id)
            .FirstOrDefaultAsync();

        if (cat > 0) return cat;

        // Fallback: first active INCOME category
        return await _context.FinancialCategories
            .Where(c => c.Type == "INCOME" && c.IsActive && c.DeletedAt == null)
            .Select(c => c.Id)
            .FirstOrDefaultAsync();
    }
}
