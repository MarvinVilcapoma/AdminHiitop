using AdminHiitop.Api.Application.DTOs.Orders;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Inventory.Entities;
using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Infrastructure.Shopify;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AdminHiitop.Api.Application.Services.Orders;

public sealed class OrderService : IOrderService
{
    private readonly IOrderQueryService      _orderQueryService;
    private readonly AdminHiitopDbContext     _context;
    private readonly IShopifyProductService  _shopifyProducts;
    private readonly ShopifyOptions          _shopifyOpts;

    public OrderService(
        IOrderQueryService     orderQueryService,
        AdminHiitopDbContext   context,
        IShopifyProductService shopifyProducts,
        IOptions<ShopifyOptions> shopifyOpts)
    {
        _orderQueryService = orderQueryService;
        _context           = context;
        _shopifyProducts   = shopifyProducts;
        _shopifyOpts       = shopifyOpts.Value;
    }

    public async Task<object> GetAsync(string? search, int? perPage, int page, bool withSummary, int? orderStatusId, int? userId = null, string? source = null, bool excludeGuideOrders = true)
    {
        if (!perPage.HasValue)
        {
            return await _orderQueryService.GetAsync(search);
        }

        IQueryable<Order> query = _context.Orders
            .AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.OrderStatus)
            .Include(item => item.DocumentType)
            .Include(item => item.DocumentPrintFormat)
            .Include(item => item.Customer)
            .Include(item => item.Invoices)
            .Include(item => item.Items)
            .Include(item => item.Warehouse)
            .Include(item => item.ShippingAgency)
            .Include(item => item.Province)
            .Include(item => item.District)
            .Include(item => item.User)
            .OrderByDescending(item => item.OrderDate)
            .ThenByDescending(item => item.Id);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term      = search.Trim();
            bool   isNumeric = int.TryParse(term, out int searchId);
            query = query.Where(item =>
                (isNumeric && item.Id == searchId) ||
                item.OrderNumber.Contains(term) ||
                (item.CustomerName != null && item.CustomerName.Contains(term)) ||
                (item.Dni          != null && item.Dni.Contains(term)) ||
                (item.Phone        != null && item.Phone.Contains(term)));
        }

        // Exclude guide-type orders so they only appear in the Guides section
        if (excludeGuideOrders)
        {
            query = query.Where(item =>
                item.DocumentType == null ||
                (item.DocumentType.Code != "GUIA_REMISION" && item.DocumentType.Code != "GUIA_REMISION_TRANSP"));
        }

        if (orderStatusId.HasValue)
            query = query.Where(item => item.OrderStatusId == orderStatusId.Value);

        if (userId.HasValue)
            query = query.Where(item => item.UserId == userId.Value);

        // source=pos means "all local orders" (both POS and form-created).
        // source=shopify is handled separately (loadShopifyOrders in frontend).
        // No additional filter needed for "pos" — all local orders are already
        // excluded from the Shopify channel by virtue of being in the local DB.

        var paged = await PaginationHelper.CreateAsync(query, page, perPage.Value);
        if (!withSummary)
        {
            return paged;
        }

        IQueryable<Order> summaryQuery = query;

        return new
        {
            paged.Data,
            paged.CurrentPage,
            paged.LastPage,
            paged.PerPage,
            paged.Total,
            summary = new
            {
                total_orders = await summaryQuery.CountAsync(),
                pending_shipping = await summaryQuery.CountAsync(item => item.OrderStatus.Slug == "pending"),
                total_revenue = await summaryQuery.SumAsync(item => item.Total)
            }
        };
    }

    public Task<Order?> GetByIdAsync(int id)
    {
        return _context.Orders
            .AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.OrderStatus)
            .Include(item => item.Customer)
            .Include(item => item.Items)
            .ThenInclude(item => item.Color)
            .Include(item => item.Invoices)
            .Include(item => item.DocumentType)
            .Include(item => item.DocumentPrintFormat)
            .Include(item => item.ShippingAgency)
            .Include(item => item.Warehouse)
            .Include(item => item.Province)
            .Include(item => item.District)
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == id);
    }

    public async Task<Order> CreateAsync(OrderUpsertRequest request)
    {
        ValidateRequest(request);

        Order entity = new()
        {
            OrderNumber = string.IsNullOrWhiteSpace(request.OrderNumber)
                ? await GenerateNextOrderNumberAsync()
                : request.OrderNumber.Trim()
        };

        if (request.OrderDate == default)
        {
            request.OrderDate = PeruClock.Now;
        }

        MapOrder(entity, request);

        _context.Orders.Add(entity);
        await _context.SaveChangesAsync();

        // Deduct Shopify inventory for items sourced from Shopify
        if (_shopifyOpts.SyncInventory)
            await DeductShopifyInventoryAsync(entity.Items);

        return entity;
    }

    private async Task DeductShopifyInventoryAsync(IEnumerable<OrderItem> items)
    {
        foreach (OrderItem item in items)
        {
            if (string.IsNullOrWhiteSpace(item.ProductKey)) continue;
            if (!item.ProductKey.StartsWith("shopify:", StringComparison.OrdinalIgnoreCase)) continue;

            // Format: shopify:{variantId}:{inventoryItemId}:{locationId}
            string[] parts = item.ProductKey.Split(':');
            if (parts.Length < 4) continue;

            if (!long.TryParse(parts[2], out long inventoryItemId)) continue;
            long.TryParse(parts[3], out long locationId);
            long resolvedLocationId = locationId > 0 ? locationId : _shopifyOpts.DefaultLocationId;
            if (resolvedLocationId <= 0) continue;

            try
            {
                await _shopifyProducts.AdjustInventoryAsync(inventoryItemId, resolvedLocationId, -item.Quantity);
            }
            catch
            {
                // Don't fail the order if Shopify adjustment fails — log in prod
            }
        }
    }

    public async Task<Order> UpdateAsync(int id, OrderUpsertRequest request)
    {
        Order entity = await _context.Orders
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new AppException("Pedido no encontrado.", 404);

        if (IsStatusOnlyUpdate(request))
        {
            entity.OrderStatusId = request.OrderStatusId;
            await _context.SaveChangesAsync();
            return entity;
        }

        ValidateRequest(request);
        request.UserId ??= entity.UserId;

        // Restore stock for items that are being removed from the order
        await RestoreStockForRemovedItemsAsync(entity, request);

        MapOrder(entity, request);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<Order> ChangeStatus(int id, int orderStatusId)
    {
        Order entity = await _context.Orders
             .Include(item => item.Items)
             .Include(item => item.OrderStatus)
             .FirstOrDefaultAsync(item => item.Id == id)
             ?? throw new AppException("Pedido no encontrado.", 404);

        string currentSlug = (entity.OrderStatus?.Slug ?? string.Empty).ToLower();
        if (currentSlug == "devuelto")
            throw new AppException("Este pedido fue devuelto y no puede cambiar de estado.", 422);

        entity.OrderStatusId = orderStatusId;
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<Order> UpdateTrackingAsync(int id, OrderTrackingUpdateRequest request)
    {
        Order entity = await _context.Orders
            .FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new AppException("Pedido no encontrado.", 404);

        entity.PickupKey        = string.IsNullOrWhiteSpace(request.PickupKey)      ? null : request.PickupKey.Trim();
        entity.TrackingNumber   = string.IsNullOrWhiteSpace(request.TrackingNumber) ? null : request.TrackingNumber.Trim();
        if (request.ShippingAgencyId.HasValue)
            entity.ShippingAgencyId = request.ShippingAgencyId.Value == 0 ? null : request.ShippingAgencyId;

        await _context.SaveChangesAsync();

        // Return with ShippingAgency loaded so the frontend can update the row
        await _context.Entry(entity).Reference(o => o.ShippingAgency).LoadAsync();
        return entity;
    }

    public async Task DeleteAsync(int id)
    {
        Order entity = await FindAsync(id);
        _context.Orders.Remove(entity);
        await _context.SaveChangesAsync();
    }

    private void MapOrder(Order entity, OrderUpsertRequest request)
    {
        entity.OrderDate = request.OrderDate;
        entity.OrderStatusId = request.OrderStatusId;
        entity.ShippingAgencyId = request.ShippingAgencyId;
        entity.PurchaseTypeId = request.PurchaseTypeId;
        entity.WarehouseId = request.WarehouseId;
        entity.Observations = request.Observations;
        entity.Phone = request.Phone;
        entity.CustomerId = request.CustomerId;
        entity.CustomerName = request.CustomerName;
        entity.Dni = request.Dni;
        entity.ProvinceId = request.ProvinceId;
        entity.DistrictId = request.DistrictId;
        entity.Address = request.Address;
        entity.PickupKey = string.IsNullOrWhiteSpace(request.PickupKey) ? null : request.PickupKey.Trim();
        entity.TrackingNumber = string.IsNullOrWhiteSpace(request.TrackingNumber) ? null : request.TrackingNumber.Trim();
        entity.DeliveryCost = request.DeliveryCost;
        entity.Total = request.Total;
        entity.DocumentTypeId = request.DocumentTypeId;
        entity.DocumentPrintFormatId = request.DocumentPrintFormatId;
        entity.DocumentNumber = request.DocumentNumber;
        entity.CustomerEmail = request.CustomerEmail;
        entity.NeedsReceipt = request.NeedsReceipt;
        entity.UserId = request.UserId;
        entity.GuideTransferReasonCode = request.GuideTransferReasonCode;
        entity.GuideTransferReasonDescription = request.GuideTransferReasonDescription;
        entity.GuideTransferMode = request.GuideTransferMode;
        entity.GuideTransferDate = request.GuideTransferDate;
        entity.GuideTotalWeight = request.GuideTotalWeight;
        entity.GuideWeightUnit = request.GuideWeightUnit;
        entity.GuidePackageCount = request.GuidePackageCount;
        entity.GuideOriginUbigeo = request.GuideOriginUbigeo;
        entity.GuideOriginAddress = request.GuideOriginAddress;
        entity.GuideDestinationUbigeo = request.GuideDestinationUbigeo;
        entity.GuideDestinationAddress = request.GuideDestinationAddress;
        entity.GuideRecipientDocType = request.GuideRecipientDocType;
        entity.GuideRecipientDocNumber = request.GuideRecipientDocNumber;
        entity.GuideRecipientName = request.GuideRecipientName;
        entity.GuideCarrierDocType = request.GuideCarrierDocType;
        entity.GuideCarrierDocNumber = request.GuideCarrierDocNumber;
        entity.GuideCarrierName = request.GuideCarrierName;
        entity.GuideVehiclePlate = request.GuideVehiclePlate;
        entity.GuideDriverDocType = request.GuideDriverDocType;
        entity.GuideDriverDocNumber = request.GuideDriverDocNumber;
        entity.GuideDriverName = request.GuideDriverName;
        entity.GuideDriverLicense = request.GuideDriverLicense;
        entity.GuideTransportCertificate = request.GuideTransportCertificate;

        entity.Items = request.Items
            .Select((item, index) => new OrderItem
            {
                ProductId = item.ProductId,
                ColorId = item.ColorId,
                CollectionId = item.CollectionId,
                ProductDescription = item.ProductDescription,
                ProductKey = item.ProductKey,
                TrackingNumber = item.TrackingNumber,
                Size = item.Size,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Subtotal = item.Subtotal,
                SortOrder = index + 1,
            })
            .ToList();
    }

    private static void ValidateRequest(OrderUpsertRequest request)
    {
        if (request.OrderStatusId <= 0)
        {
            throw new AppException("El estado del pedido es obligatorio.", 400);
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            throw new AppException("Agrega al menos un item al pedido.", 400);
        }

        // Guide orders (GRE) identify products by description — product ID and Shopify key are optional.
        bool isGuideOrder = !string.IsNullOrWhiteSpace(request.GuideTransferReasonCode)
                         || !string.IsNullOrWhiteSpace(request.GuideOriginUbigeo);

        if (!isGuideOrder && request.Items.Any(item =>
            (item.ProductId == null || item.ProductId <= 0) &&
            (string.IsNullOrWhiteSpace(item.ProductKey) ||
             !item.ProductKey.StartsWith("shopify:", StringComparison.OrdinalIgnoreCase))))
        {
            throw new AppException("Todos los items deben tener un producto o una referencia Shopify válida.", 400);
        }
    }

    private static bool IsStatusOnlyUpdate(OrderUpsertRequest request)
    {
        return request.OrderStatusId > 0
            && (request.Items is null || request.Items.Count == 0)
            && string.IsNullOrWhiteSpace(request.OrderNumber)
            && request.OrderDate == default
            && !request.ShippingAgencyId.HasValue
            && !request.PurchaseTypeId.HasValue
            && !request.WarehouseId.HasValue
            && string.IsNullOrWhiteSpace(request.Observations)
            && string.IsNullOrWhiteSpace(request.Phone)
            && !request.CustomerId.HasValue
            && string.IsNullOrWhiteSpace(request.CustomerName)
            && string.IsNullOrWhiteSpace(request.Dni)
            && !request.ProvinceId.HasValue
            && !request.DistrictId.HasValue
            && string.IsNullOrWhiteSpace(request.Address)
            && request.DeliveryCost == default
            && request.Total == default
            && !request.DocumentTypeId.HasValue
            && !request.DocumentPrintFormatId.HasValue
            && string.IsNullOrWhiteSpace(request.DocumentNumber)
            && string.IsNullOrWhiteSpace(request.CustomerEmail)
            && !request.NeedsReceipt
            && !request.UserId.HasValue
            && string.IsNullOrWhiteSpace(request.GuideTransferReasonCode)
            && string.IsNullOrWhiteSpace(request.GuideTransferReasonDescription)
            && string.IsNullOrWhiteSpace(request.GuideTransferMode)
            && !request.GuideTransferDate.HasValue
            && !request.GuideTotalWeight.HasValue
            && string.IsNullOrWhiteSpace(request.GuideWeightUnit)
            && !request.GuidePackageCount.HasValue
            && string.IsNullOrWhiteSpace(request.GuideOriginUbigeo)
            && string.IsNullOrWhiteSpace(request.GuideOriginAddress)
            && string.IsNullOrWhiteSpace(request.GuideDestinationUbigeo)
            && string.IsNullOrWhiteSpace(request.GuideDestinationAddress)
            && string.IsNullOrWhiteSpace(request.GuideRecipientDocType)
            && string.IsNullOrWhiteSpace(request.GuideRecipientDocNumber)
            && string.IsNullOrWhiteSpace(request.GuideRecipientName)
            && string.IsNullOrWhiteSpace(request.GuideCarrierDocType)
            && string.IsNullOrWhiteSpace(request.GuideCarrierDocNumber)
            && string.IsNullOrWhiteSpace(request.GuideCarrierName)
            && string.IsNullOrWhiteSpace(request.GuideVehiclePlate)
            && string.IsNullOrWhiteSpace(request.GuideDriverDocType)
            && string.IsNullOrWhiteSpace(request.GuideDriverDocNumber)
            && string.IsNullOrWhiteSpace(request.GuideDriverName)
            && string.IsNullOrWhiteSpace(request.GuideDriverLicense)
            && string.IsNullOrWhiteSpace(request.GuideTransportCertificate);
    }

    public async Task<IReadOnlyList<OrderMonthlyStat>> GetMonthlyStatsAsync(int year)
    {
        string[] monthLabels = ["Ene","Feb","Mar","Abr","May","Jun","Jul","Ago","Sep","Oct","Nov","Dic"];

        var rows = await _context.Orders
            .AsNoTracking()
            .Where(o => o.OrderDate.Year == year)
            .GroupBy(o => o.OrderDate.Month)
            .Select(g => new { Month = g.Key, Orders = g.Count(), Revenue = g.Sum(o => o.Total) })
            .OrderBy(x => x.Month)
            .ToListAsync();

        return rows.Select(r => new OrderMonthlyStat
        {
            Year    = year,
            Month   = r.Month,
            Label   = monthLabels[r.Month - 1],
            Orders  = r.Orders,
            Revenue = r.Revenue,
        }).ToList();
    }

    /// <summary>
    /// When an order update removes items, restore their stock so inventory is correct.
    /// Only applies to local (non-Shopify) products.
    /// </summary>
    private async Task RestoreStockForRemovedItemsAsync(Order existing, OrderUpsertRequest request)
    {
        if (existing.WarehouseId is null) return;
        int warehouseId = existing.WarehouseId.Value;

        var newProductIds = request.Items
            .Where(i => i.ProductId.HasValue)
            .Select(i => i.ProductId!.Value)
            .ToHashSet();

        var removedItems = existing.Items
            .Where(i => i.ProductId.HasValue && !newProductIds.Contains(i.ProductId!.Value))
            .ToList();

        foreach (OrderItem removed in removedItems)
        {
            Stock? stock = await _context.Stocks.FirstOrDefaultAsync(s =>
                s.ProductId    == removed.ProductId!.Value
                && s.WarehouseId == warehouseId
                && s.ColorId    == removed.ColorId
                && s.Size       == removed.Size);

            // Relax criteria if exact variant not found
            stock ??= await _context.Stocks.FirstOrDefaultAsync(s =>
                s.ProductId == removed.ProductId!.Value && s.WarehouseId == warehouseId
                && s.Size == removed.Size);

            stock ??= await _context.Stocks.FirstOrDefaultAsync(s =>
                s.ProductId == removed.ProductId!.Value && s.WarehouseId == warehouseId);

            if (stock is null) continue;
            stock.Quantity += removed.Quantity;
        }
    }

    private async Task<Order> FindAsync(int id)
    {
        Order? entity = await _context.Orders.FirstOrDefaultAsync(item => item.Id == id);
        if (entity is null)
        {
            throw new AppException("Pedido no encontrado.", 404);
        }

        return entity;
    }

    private async Task<string> GenerateNextOrderNumberAsync()
    {
        List<string> orderNumbers = await _context.Orders
            .AsNoTracking()
            .Select(item => item.OrderNumber)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToListAsync();

        int nextNumber = orderNumbers
            .Select(item => int.TryParse(item, out int parsed) ? parsed : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        return nextNumber.ToString();
    }
}
