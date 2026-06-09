using AdminHiitop.Api.Application.DTOs.Finance;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IOrderFinanceIntegrationService
{
    /// <summary>
    /// Generates (or updates) a FinancialMovement for the given order.
    /// Idempotent: calling it multiple times on the same order does NOT create duplicates.
    /// Returns null when the order should not generate income (e.g. cancelled orders).
    /// </summary>
    Task<int?> GenerateFromOrderAsync(int orderId, int? userId = null);

    /// <summary>
    /// Creates an adjustment (reversal) movement when an order is cancelled or refunded.
    /// Links to the original movement via ParentMovementId.
    /// </summary>
    Task CreateAdjustmentAsync(int originalMovementId, string reason, int? userId = null);

    /// <summary>
    /// Scans all eligible orders and generates missing FinancialMovements.
    /// Skips orders that already have a movement (idempotent batch process).
    /// </summary>
    Task<SyncOrdersResponse> SyncAllOrdersAsync(int? userId = null);
}
