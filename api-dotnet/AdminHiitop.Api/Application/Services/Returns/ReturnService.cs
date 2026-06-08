using AdminHiitop.Api.Application.DTOs.Returns;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Domain.Inventory.Entities;
using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Infrastructure.Shopify;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using InvoiceSeriesEntity = AdminHiitop.Api.Domain.Sales.Entities.InvoiceSeries;

namespace AdminHiitop.Api.Application.Services.Returns;

public sealed class ReturnService : IReturnService
{
    private readonly AdminHiitopDbContext _context;
    private readonly IInvoiceElectronicBillingService _billingService;
    private readonly IInvoiceSeriesService _seriesService;
    private readonly IShopifyProductService _shopify;
    private readonly ShopifyOptions _shopifyOpts;

    private static readonly string[] ElectronicDocTypes = ["01", "03", "07", "08"];

    private static readonly Dictionary<string, string> ReturnTypeLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FULL_REFUND"]                = "Devolución total",
        ["PARTIAL_REFUND"]             = "Devolución parcial",
        ["EXCHANGE_SAME_PRICE"]        = "Cambio mismo precio",
        ["EXCHANGE_WITH_EXTRA_PAYMENT"]= "Cambio con pago adicional",
        ["EXCHANGE_WITH_REFUND"]       = "Cambio con diferencia a favor",
        ["STORE_CREDIT"]               = "Saldo a favor",
    };

    private static readonly Dictionary<string, string> StatusLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["REQUESTED"]          = "Solicitado",
        ["APPROVED"]           = "Aprobado",
        ["CREDIT_NOTE_ISSUED"] = "Nota de crédito emitida",
        ["COMPLETED"]          = "Completado",
        ["CANCELLED"]          = "Cancelado",
    };

    public ReturnService(
        AdminHiitopDbContext context,
        IInvoiceElectronicBillingService billingService,
        IInvoiceSeriesService seriesService,
        IShopifyProductService shopify,
        IOptions<ShopifyOptions> shopifyOpts)
    {
        _context = context;
        _billingService = billingService;
        _seriesService = seriesService;
        _shopify = shopify;
        _shopifyOpts = shopifyOpts.Value;
    }

    // ── Query ─────────────────────────────────────────────────────────────────

    public async Task<object> GetAllAsync(int perPage, int page, string? search = null)
    {
        IQueryable<ReturnRequest> query = _context.ReturnRequests
            .AsNoTracking()
            .Include(r => r.Order)
            .Include(r => r.Customer)
            .Include(r => r.OriginalInvoice)
            .Include(r => r.CreditNoteInvoice)
            .OrderByDescending(r => r.CreatedAt);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(r =>
                (r.Order != null && r.Order.OrderNumber.Contains(term)) ||
                (r.OriginalInvoice != null && r.OriginalInvoice.FullNumber.Contains(term)) ||
                (r.Customer != null && r.Customer.FullName.Contains(term)) ||
                (r.Reason != null && r.Reason.Contains(term)));
        }

        // Materialize first; label computation requires C# Dictionary lookups that EF cannot translate to SQL.
        var raw = await PaginationHelper.CreateAsync(query.Select(r => new
        {
            r.Id,
            r.OrderId,
            order_number            = r.Order != null ? r.Order.OrderNumber : null,
            r.CustomerId,
            customer_name           = r.Customer != null ? r.Customer.FullName : null,
            r.OriginalInvoiceId,
            original_invoice_number = r.OriginalInvoice != null ? r.OriginalInvoice.FullNumber : null,
            r.CreditNoteInvoiceId,
            credit_note_number      = r.CreditNoteInvoice != null ? r.CreditNoteInvoice.FullNumber : null,
            credit_note_pdf_url     = r.CreditNoteInvoice != null ? r.CreditNoteInvoice.PdfUrl : null,
            r.ReturnType,
            r.Status,
            r.Reason,
            r.TotalReturnedAmount,
            r.RefundAmount,
            r.StoreCreditAmount,
            r.RequiresCreditNote,
            r.CreatedAt,
            r.CompletedAt
        }), page, perPage);

        // Add labels in memory after materialization
        var dataWithLabels = raw.Data.Select(r => new
        {
            r.Id,
            r.OrderId,
            r.order_number,
            r.CustomerId,
            r.customer_name,
            r.OriginalInvoiceId,
            r.original_invoice_number,
            r.CreditNoteInvoiceId,
            r.credit_note_number,
            r.credit_note_pdf_url,
            r.ReturnType,
            return_type_label       = ReturnTypeLabels.GetValueOrDefault(r.ReturnType, r.ReturnType),
            r.Status,
            status_label            = StatusLabels.GetValueOrDefault(r.Status, r.Status),
            r.Reason,
            r.TotalReturnedAmount,
            r.RefundAmount,
            r.StoreCreditAmount,
            r.RequiresCreditNote,
            r.CreatedAt,
            r.CompletedAt
        }).ToList();

        return new
        {
            data         = dataWithLabels,
            current_page = raw.CurrentPage,
            last_page    = raw.LastPage,
            per_page     = raw.PerPage,
            total        = raw.Total
        };
    }

    public async Task<ReturnRequestResponse?> GetByIdAsync(int id)
    {
        ReturnRequest? rr = await _context.ReturnRequests
            .AsNoTracking()
            .Include(r => r.Order)
            .Include(r => r.Customer)
            .Include(r => r.OriginalInvoice)
            .Include(r => r.CreditNoteInvoice)
            .Include(r => r.Items)
            .Include(r => r.CustomerCredit)
            .FirstOrDefaultAsync(r => r.Id == id);

        return rr is null ? null : MapToResponse(rr);
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<ReturnRequestResponse> CreateReturnAsync(CreateReturnRequest request)
    {
        Order? order = await _context.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId)
            ?? throw new AppException("Pedido no encontrado.", 404);

        // Block if order already has any active (non-cancelled) return
        bool hasActiveReturn = await _context.ReturnRequests
            .AnyAsync(r => r.OrderId == request.OrderId && r.Status != "CANCELLED");

        if (hasActiveReturn)
            throw new AppException("Este pedido ya tiene una devolución registrada. No se pueden registrar más devoluciones.", 422);

        // Validate items belong to the order and quantities are valid
        foreach (CreateReturnItemRequest item in request.Items)
        {
            if (item.Quantity <= 0)
                throw new AppException("La cantidad a devolver debe ser mayor a cero.", 422);

            if (item.OrderItemId.HasValue)
            {
                OrderItem? orderItem = order.Items.FirstOrDefault(i => i.Id == item.OrderItemId.Value);
                if (orderItem is null)
                    throw new AppException($"El ítem de pedido {item.OrderItemId} no pertenece al pedido {order.Id}.", 422);

                int alreadyReturned = await AlreadyReturnedQtyAsync(item.OrderItemId.Value);
                if (alreadyReturned + item.Quantity > orderItem.Quantity)
                {
                    throw new AppException(
                        $"No se puede devolver {item.Quantity} unidad(es) de '{orderItem.ProductDescription}'. " +
                        $"Solo quedan {orderItem.Quantity - alreadyReturned} disponibles para devolución.", 422);
                }
            }
        }

        decimal totalReturned = request.Items.Sum(i => i.Quantity * i.UnitPrice);

        // Determine if a credit note is required
        Invoice? originalInvoice = null;
        if (request.OriginalInvoiceId.HasValue)
        {
            originalInvoice = await _context.Invoices
                .FirstOrDefaultAsync(i => i.Id == request.OriginalInvoiceId.Value);
        }
        else
        {
            // Find the most recent accepted invoice linked to this order
            originalInvoice = await _context.Invoices
                .Where(i => i.OrderId == order.Id && i.Status == "accepted" && ElectronicDocTypes.Contains(i.DocType))
                .OrderByDescending(i => i.IssuedAt)
                .FirstOrDefaultAsync();
        }

        bool requiresCreditNote = originalInvoice is not null
            && ElectronicDocTypes.Contains(originalInvoice.DocType)
            && originalInvoice.DocType is not "07" and not "08"; // not already a NC/ND

        // Persist the return request
        ReturnRequest returnRequest = new()
        {
            OrderId              = order.Id,
            CustomerId           = request.CustomerId ?? order.CustomerId,
            OriginalInvoiceId    = originalInvoice?.Id,
            ReturnType           = request.ReturnType,
            Status               = "REQUESTED",
            Reason               = request.Reason,
            Observation          = request.Observation,
            TotalReturnedAmount  = totalReturned,
            RefundAmount         = request.ReturnType is "FULL_REFUND" or "PARTIAL_REFUND" or "EXCHANGE_WITH_REFUND"
                                   ? totalReturned : 0,
            StoreCreditAmount    = request.ReturnType is "STORE_CREDIT" ? totalReturned : 0,
            RequiresCreditNote   = requiresCreditNote,
            AutoEmitCreditNote   = request.AutoEmitCreditNote,
        };

        foreach (CreateReturnItemRequest item in request.Items)
        {
            // Auto-resolve ProductId from the order item when not supplied by the caller.
            // Without this, UpdateStockAsync skips the item and stock never gets updated.
            int? resolvedProductId = item.ProductId;
            if (!resolvedProductId.HasValue && item.OrderItemId.HasValue)
            {
                OrderItem? oi = order.Items.FirstOrDefault(i => i.Id == item.OrderItemId.Value);
                resolvedProductId = oi?.ProductId;
            }

            returnRequest.Items.Add(new ReturnRequestItem
            {
                OrderItemId        = item.OrderItemId,
                ProductId          = resolvedProductId,
                StockId            = item.StockId,
                Quantity           = item.Quantity,
                UnitPrice          = item.UnitPrice,
                TotalAmount        = item.Quantity * item.UnitPrice,
                ProductDescription = item.ProductDescription,
                Condition          = item.Condition,
                RestockAction      = item.RestockAction,
                Reason             = item.Reason
            });
        }

        _context.ReturnRequests.Add(returnRequest);
        await _context.SaveChangesAsync();

        // Update local stock and push to Shopify if sync is enabled
        List<string> stockWarnings = await UpdateStockAsync(returnRequest, order);

        // Generate store credit if applicable
        if (returnRequest.StoreCreditAmount > 0 && returnRequest.CustomerId.HasValue)
        {
            await CreateCustomerCreditAsync(returnRequest);
        }

        // Emit credit note via Nubefact if required and auto-emit is on
        if (requiresCreditNote && request.AutoEmitCreditNote && originalInvoice is not null)
        {
            try { await IssueCreditNoteInternalAsync(returnRequest, originalInvoice, request.NoteMotive, request.NoteMotiveDesc); }
            catch (Exception ex)
            {
                returnRequest.Status = "CREDIT_NOTE_PENDING";
                returnRequest.Observation = (returnRequest.Observation ?? "") +
                    $" [NC pendiente: {ex.Message}]";
                await _context.SaveChangesAsync();
            }
        }
        else if (!requiresCreditNote)
        {
            returnRequest.Status = "COMPLETED";
            returnRequest.CompletedAt = PeruClock.Now;
            await _context.SaveChangesAsync();
        }

        // Update the order status to "devuelto" so no further actions are allowed
        await MarkOrderAsReturnedAsync(order);

        ReturnRequest? saved = await _context.ReturnRequests
            .AsNoTracking()
            .Include(r => r.Order).Include(r => r.Customer)
            .Include(r => r.OriginalInvoice).Include(r => r.CreditNoteInvoice)
            .Include(r => r.Items).Include(r => r.CustomerCredit)
            .FirstOrDefaultAsync(r => r.Id == returnRequest.Id);

        return MapToResponse(saved!, stockWarnings);
    }

    // ── Issue credit note manually ─────────────────────────────────────────────

    public async Task<ReturnRequestResponse> IssueCreditNoteAsync(int returnRequestId)
    {
        ReturnRequest rr = await _context.ReturnRequests
            .Include(r => r.OriginalInvoice)
            .FirstOrDefaultAsync(r => r.Id == returnRequestId)
            ?? throw new AppException("Solicitud de devolución no encontrada.", 404);

        if (rr.CreditNoteInvoiceId.HasValue)
            throw new AppException("Ya se emitió una nota de crédito para esta devolución.", 422);

        if (rr.OriginalInvoice is null)
            throw new AppException("Esta devolución no tiene comprobante original asociado.", 422);

        await IssueCreditNoteInternalAsync(rr, rr.OriginalInvoice, "06", "Devolución por devolución");

        // Also mark the order as returned
        if (rr.OrderId.HasValue)
        {
            Order? ord = await _context.Orders.FirstOrDefaultAsync(o => o.Id == rr.OrderId.Value);
            if (ord is not null) await MarkOrderAsReturnedAsync(ord);
        }

        return (await GetByIdAsync(returnRequestId))!;
    }

    public async Task<object> GetCustomerCreditsAsync(int perPage, int page, int? customerId = null)
    {
        IQueryable<CustomerCredit> query = _context.CustomerCredits
            .AsNoTracking()
            .Include(c => c.Customer)
            .OrderByDescending(c => c.CreatedAt);

        if (customerId.HasValue)
            query = query.Where(c => c.CustomerId == customerId.Value);

        return await PaginationHelper.CreateAsync(query.Select(c => new
        {
            c.Id,
            c.CustomerId,
            customer_name = c.Customer.FullName,
            c.ReturnRequestId,
            c.CreditNoteInvoiceId,
            c.Amount,
            c.UsedAmount,
            c.RemainingAmount,
            c.Status,
            c.Notes,
            c.ExpiresAt,
            c.CreatedAt
        }), page, perPage);
    }

    public async Task<object> CancelReturnAsync(int id, string? reason)
    {
        ReturnRequest rr = await _context.ReturnRequests
            .Include(r => r.Items)
            .Include(r => r.Order)
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new AppException("Solicitud de devolución no encontrada.", 404);

        if (rr.Status is "COMPLETED" or "CANCELLED")
            throw new AppException("No se puede cancelar una solicitud ya completada o cancelada.", 422);

        // Reverse the stock that was added when the return was created.
        int warehouseId = rr.Order?.WarehouseId ?? await GetDefaultWarehouseIdAsync();

        foreach (ReturnRequestItem item in rr.Items)
        {
            if (item.RestockAction == "DO_NOT_RESTOCK" || !item.ProductId.HasValue)
                continue;

            Stock? stock = await FindStockVariantAsync(item, rr.Order!, warehouseId);
            if (stock is null) continue;

            int previousQty = stock.Quantity;
            stock.Quantity = Math.Max(0, stock.Quantity - item.Quantity);

            _context.StockMovements.Add(new StockMovement
            {
                StockId          = stock.Id,
                ProductId        = stock.ProductId,
                WarehouseId      = stock.WarehouseId,
                ColorId          = stock.ColorId,
                Size             = stock.Size,
                MovementType     = "RETURN_CANCEL",
                Quantity         = item.Quantity,
                PreviousQuantity = previousQty,
                NewQuantity      = stock.Quantity,
                Reference        = $"RETURN-CANCEL-{rr.Id}",
                Notes            = $"Anulación de devolución #{rr.Id}. Motivo: {reason ?? "sin motivo"}",
            });
        }

        rr.Status = "CANCELLED";
        rr.CancelledAt = PeruClock.Now;
        rr.CancellationReason = reason;
        await _context.SaveChangesAsync();

        return new { success = true, message = "Solicitud de devolución cancelada y stock revertido." };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<int> AlreadyReturnedQtyAsync(int orderItemId)
    {
        return await _context.ReturnRequestItems
            .Where(i => i.OrderItemId == orderItemId &&
                        i.ReturnRequest.Status != "CANCELLED")
            .SumAsync(i => i.Quantity);
    }

    private async Task<List<string>> UpdateStockAsync(ReturnRequest returnRequest, Order order)
    {
        var warnings    = new List<string>();
        int defaultWh   = order.WarehouseId ?? await GetDefaultWarehouseIdAsync();

        foreach (ReturnRequestItem item in returnRequest.Items)
        {
            if (item.RestockAction == "DO_NOT_RESTOCK" || !item.ProductId.HasValue)
                continue;

            OrderItem? srcOrderItem = item.OrderItemId.HasValue
                ? order.Items.FirstOrDefault(i => i.Id == item.OrderItemId.Value)
                : null;

            // Resolve the warehouse: order warehouse → Shopify location mapping → default
            int warehouseId = order.WarehouseId
                ?? await ResolveWarehouseFromProductKeyAsync(srcOrderItem?.ProductKey)
                ?? defaultWh;

            Stock? stock = await FindStockVariantAsync(item, order, warehouseId);

            // If not found with the specific warehouse, try default as last resort before creating
            if (stock is null && warehouseId != defaultWh)
                stock = await FindStockVariantAsync(item, order, defaultWh);

            // If no stock record exists anywhere, create one at the correct warehouse
            if (stock is null)
            {
                stock = new Stock
                {
                    ProductId   = item.ProductId.Value,
                    WarehouseId = warehouseId,
                    ColorId     = srcOrderItem?.ColorId,
                    Size        = srcOrderItem?.Size,
                    Quantity    = 0,
                };
                _context.Stocks.Add(stock);
                await _context.SaveChangesAsync(); // flush to get stock.Id
            }

            int previousQty = stock.Quantity;
            stock.Quantity += item.Quantity;

            string movementType = item.RestockAction == "SEND_TO_REVIEW" ? "RETURN_REVIEW" : "RETURN_IN";

            _context.StockMovements.Add(new StockMovement
            {
                StockId          = stock.Id,
                ProductId        = stock.ProductId,
                WarehouseId      = stock.WarehouseId,
                ColorId          = stock.ColorId,
                Size             = stock.Size,
                MovementType     = movementType,
                Quantity         = item.Quantity,
                PreviousQuantity = previousQty,
                NewQuantity      = stock.Quantity,
                Reference        = $"RETURN-{returnRequest.Id}",
                Notes            = $"Devolución de {item.ProductDescription ?? "producto"}. Condición: {item.Condition}"
            });
        }

        await _context.SaveChangesAsync();

        // Push inventory adjustments to Shopify (fire-and-forget, never fail the return)
        if (_shopifyOpts.SyncInventory)
            await RestoreShopifyInventoryAsync(returnRequest, order);

        return warnings;
    }

    private async Task RestoreShopifyInventoryAsync(ReturnRequest returnRequest, Order order)
    {
        foreach (ReturnRequestItem item in returnRequest.Items)
        {
            if (item.RestockAction != "RETURN_TO_STOCK") continue;

            OrderItem? srcOrderItem = item.OrderItemId.HasValue
                ? order.Items.FirstOrDefault(i => i.Id == item.OrderItemId.Value)
                : null;

            string? productKey = srcOrderItem?.ProductKey;
            if (string.IsNullOrWhiteSpace(productKey)) continue;
            if (!productKey.StartsWith("shopify:", StringComparison.OrdinalIgnoreCase)) continue;

            // Format: shopify:{variantId}:{inventoryItemId}:{locationId}
            string[] parts = productKey.Split(':');
            if (parts.Length < 4) continue;
            if (!long.TryParse(parts[2], out long inventoryItemId)) continue;
            long.TryParse(parts[3], out long locationId);
            long resolvedLocationId = locationId > 0 ? locationId : _shopifyOpts.DefaultLocationId;
            if (resolvedLocationId <= 0) continue;

            try
            {
                await _shopify.AdjustInventoryAsync(inventoryItemId, resolvedLocationId, +item.Quantity);
            }
            catch
            {
                // Don't fail the return if Shopify sync fails — consistent with OrderService pattern
            }
        }
    }

    /// <summary>
    /// Tries to find the stock variant for a return item using progressively relaxed criteria
    /// so that null ColorId / null Size on the OrderItem never causes a silent miss.
    /// </summary>
    private async Task<Stock?> FindStockVariantAsync(ReturnRequestItem item, Order order, int warehouseId)
    {
        int productId = item.ProductId!.Value;

        // 1. Explicit stock ID supplied by caller (most precise).
        if (item.StockId.HasValue)
            return await _context.Stocks.FirstOrDefaultAsync(s => s.Id == item.StockId.Value);

        OrderItem? srcOrderItem = item.OrderItemId.HasValue
            ? order.Items.FirstOrDefault(i => i.Id == item.OrderItemId.Value)
            : null;

        int? colorId = srcOrderItem?.ColorId;
        string? size  = srcOrderItem?.Size;

        // 2. Exact match: product + warehouse + color + size.
        if (colorId.HasValue && !string.IsNullOrWhiteSpace(size))
        {
            var exact = await _context.Stocks.FirstOrDefaultAsync(s =>
                s.ProductId == productId && s.WarehouseId == warehouseId
                && s.ColorId == colorId && s.Size == size);
            if (exact is not null) return exact;
        }

        // 3. Match by size only (ignore color — color may not be set on old OrderItems).
        if (!string.IsNullOrWhiteSpace(size))
        {
            var bySize = await _context.Stocks.FirstOrDefaultAsync(s =>
                s.ProductId == productId && s.WarehouseId == warehouseId && s.Size == size);
            if (bySize is not null) return bySize;
        }

        // 4. Match by color only (ignore size).
        if (colorId.HasValue)
        {
            var byColor = await _context.Stocks.FirstOrDefaultAsync(s =>
                s.ProductId == productId && s.WarehouseId == warehouseId && s.ColorId == colorId);
            if (byColor is not null) return byColor;
        }

        // 5. Any stock for this product + warehouse (last resort).
        return await _context.Stocks.FirstOrDefaultAsync(s =>
            s.ProductId == productId && s.WarehouseId == warehouseId);
    }

    /// <summary>
    /// Parses the Shopify ProductKey to find the local warehouse mapped to that Shopify location.
    /// Format: shopify:{variantId}:{inventoryItemId}:{shopifyLocationId}
    /// Returns null when the key is absent, not a Shopify key, or no mapping exists.
    /// </summary>
    private async Task<int?> ResolveWarehouseFromProductKeyAsync(string? productKey)
    {
        if (string.IsNullOrWhiteSpace(productKey)) return null;
        if (!productKey.StartsWith("shopify:", StringComparison.OrdinalIgnoreCase)) return null;

        string[] parts = productKey.Split(':');
        if (parts.Length < 4 || !long.TryParse(parts[3], out long shopifyLocationId) || shopifyLocationId <= 0)
            return null;

        var loc = await _context.ShopifyLocations
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.ShopifyLocationId == shopifyLocationId);

        return loc?.LocalWarehouseId; // null when no mapping configured
    }

    private async Task<int> GetDefaultWarehouseIdAsync()
    {
        int? id = await _context.Warehouses
            .Where(w => w.IsActive != false)
            .OrderBy(w => w.Id)
            .Select(w => (int?)w.Id)
            .FirstOrDefaultAsync();
        return id ?? 1;
    }

    /// <summary>
    /// Sets the order's status to "devuelto" so no further edits or actions are allowed.
    /// </summary>
    private async Task MarkOrderAsReturnedAsync(Order order)
    {
        int? devueltoId = await _context.OrderStatuses
            .Where(s => s.Slug == "devuelto" && s.IsActive != false)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync();

        if (devueltoId is null) return; // status not seeded — skip silently

        order.OrderStatusId = devueltoId.Value;
        await _context.SaveChangesAsync();
    }

    private async Task CreateCustomerCreditAsync(ReturnRequest returnRequest)
    {
        _context.CustomerCredits.Add(new CustomerCredit
        {
            CustomerId           = returnRequest.CustomerId!.Value,
            ReturnRequestId      = returnRequest.Id,
            Amount               = returnRequest.StoreCreditAmount,
            UsedAmount           = 0,
            RemainingAmount      = returnRequest.StoreCreditAmount,
            Status               = "ACTIVE",
            Notes                = $"Saldo generado por devolución #{returnRequest.Id}"
        });
        await _context.SaveChangesAsync();
    }

    private async Task IssueCreditNoteInternalAsync(
        ReturnRequest returnRequest, Invoice originalInvoice,
        string noteMotive, string? noteMotiveDesc)
    {
        // Nubefact assigns the same serie code for NC as for the base document
        // (e.g., FFF1 Factura → FFF1 NC, BBB1 Boleta → BBB1 NC).
        // Look up by matching serie + doc_type "07".
        string originalSerie = originalInvoice.Serie ?? string.Empty;
        InvoiceSeriesEntity? ncSeries = await _context.InvoiceSeries
            .AsNoTracking()
            .Where(s => s.IsActive && s.DocType == "07" && s.Serie == originalSerie)
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync()
            ?? throw new AppException(
                $"No hay serie de Nota de Crédito activa para '{originalSerie}' (tipo 07). " +
                "Créala en Parámetros fiscales.", 422);

        (string ncSerie, int ncCorrelativo) = await _seriesService.GetNextAsync(ncSeries.Id);

        // Recalculate amounts based on the returned amount (not full invoice)
        decimal returnedTotal = returnRequest.TotalReturnedAmount;
        decimal returnedBase  = Math.Round(returnedTotal / 1.18m, 2);
        decimal returnedIgv   = Math.Round(returnedTotal - returnedBase, 2);

        // NubeFact tipo de documento: "1" = Factura, "2" = Boleta (string-typed field)
        string nubefactDocTipo = originalInvoice.DocType == "01" ? "1" : "2";

        var creditNote = new Invoice
        {
            InvoiceSeriesId   = ncSeries.Id,
            DocType           = "07",
            Serie             = ncSerie,
            Correlativo       = ncCorrelativo,
            FullNumber        = $"{ncSerie}-{ncCorrelativo:00000000}",
            Status            = "draft",
            OrderId           = originalInvoice.OrderId,
            CustomerDocType   = originalInvoice.CustomerDocType,
            CustomerDocNumber = originalInvoice.CustomerDocNumber,
            CustomerName      = originalInvoice.CustomerName,
            CustomerPhone     = originalInvoice.CustomerPhone,
            CustomerEmail     = originalInvoice.CustomerEmail,
            Currency          = originalInvoice.Currency,
            FormOfPayment     = "contado",
            MtoOperGravadas   = returnedBase,
            MtoIgv            = returnedIgv,
            ValorVenta        = returnedBase,
            SubTotal          = returnedTotal,
            MtoImpVenta       = returnedTotal,
            IssuedAt          = PeruClock.Now,
            NoteMotive        = noteMotive,
            NoteMotiveDesc    = noteMotiveDesc ?? GetMotiveDesc(noteMotive),
            RefDocType        = nubefactDocTipo,   // "1" or "2" as NubeFact expects
            RefDocNumber      = originalInvoice.FullNumber,
            RefDocDate        = originalInvoice.IssuedAt,
            SunatDescription  = "Nota de crédito generada."
        };

        _context.Invoices.Add(creditNote);
        await _context.SaveChangesAsync();

        var sendResult = await _billingService.SendCreditNoteAsync(creditNote.Id);

        returnRequest.CreditNoteInvoiceId = creditNote.Id;
        returnRequest.Status = sendResult.Success ? "CREDIT_NOTE_ISSUED" : "CREDIT_NOTE_PENDING";

        if (sendResult.Success)
        {
            returnRequest.Status = "CREDIT_NOTE_ISSUED";
            returnRequest.CompletedAt = PeruClock.Now;
        }

        await _context.SaveChangesAsync();
    }

    private static string GetMotiveDesc(string code) => code switch
    {
        "01" => "Anulación de la operación",
        "06" => "Devolución total",
        "07" => "Devolución por ítem",
        _ => "Nota de crédito"
    };

    private static ReturnRequestResponse MapToResponse(ReturnRequest rr, List<string>? stockWarnings = null) => new()
    {
        Id                      = rr.Id,
        OrderId                 = rr.OrderId,
        CustomerId              = rr.CustomerId,
        CustomerName            = rr.Customer?.FullName,
        CustomerDni             = rr.Customer?.Dni ?? rr.Customer?.Ruc,
        OriginalInvoiceId       = rr.OriginalInvoiceId,
        OriginalInvoiceNumber   = rr.OriginalInvoice?.FullNumber,
        CreditNoteInvoiceId     = rr.CreditNoteInvoiceId,
        CreditNoteNumber        = rr.CreditNoteInvoice?.FullNumber,
        ReturnType              = rr.ReturnType,
        ReturnTypeLabel         = ReturnTypeLabels.GetValueOrDefault(rr.ReturnType, rr.ReturnType),
        Status                  = rr.Status,
        StatusLabel             = StatusLabels.GetValueOrDefault(rr.Status, rr.Status),
        Reason                  = rr.Reason,
        Observation             = rr.Observation,
        TotalReturnedAmount     = rr.TotalReturnedAmount,
        RefundAmount            = rr.RefundAmount,
        StoreCreditAmount       = rr.StoreCreditAmount,
        RequiresCreditNote      = rr.RequiresCreditNote,
        CreatedAt               = rr.CreatedAt,
        CompletedAt             = rr.CompletedAt,
        CreditNotePdfUrl        = rr.CreditNoteInvoice?.PdfUrl,
        CreditNoteSunatStatus   = rr.CreditNoteInvoice?.SunatDescription,
        StockWarnings           = stockWarnings ?? [],
        Items = rr.Items.Select(i => new ReturnItemResponse
        {
            Id                 = i.Id,
            OrderItemId        = i.OrderItemId,
            ProductId          = i.ProductId,
            ProductDescription = i.ProductDescription,
            Quantity           = i.Quantity,
            UnitPrice          = i.UnitPrice,
            TotalAmount        = i.TotalAmount,
            Condition          = i.Condition,
            RestockAction      = i.RestockAction,
            Reason             = i.Reason
        }).ToList()
    };
}
