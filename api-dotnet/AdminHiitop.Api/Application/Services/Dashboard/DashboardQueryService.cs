using System.Linq.Expressions;
using System.Text.RegularExpressions;
using AdminHiitop.Api.Application.DTOs.Dashboard;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Dashboard;

public sealed class DashboardQueryService : IDashboardQueryService
{
    private static readonly Regex PosPaymentMethodRegex =
        new(@"POS\s*·\s*Metodo de pago:\s*([^|]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly AdminHiitopDbContext _context;

    public DashboardQueryService(AdminHiitopDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardSummaryResponse> GetSummaryAsync(DashboardSummaryFilterRequest request)
    {
        DashboardProductsResponse products = await GetProductsAsync(request);
        DashboardCustomersResponse customers = await GetCustomersAsync(request);
        DashboardOrdersResponse orders = await GetOrdersAsync(request);
        DashboardInvoicesResponse invoices = await GetInvoicesAsync(request);

        return new DashboardSummaryResponse
        {
            TotalProducts = products.TotalProducts,
            ActiveProducts = products.ActiveProducts,
            TotalCustomers = customers.TotalCustomers,
            ActiveCustomers = customers.ActiveCustomers,
            TotalOrders = orders.TotalOrders,
            PendingOrders = orders.PendingOrders,
            TotalInvoices = invoices.TotalInvoices,
            PendingInvoices = invoices.PendingInvoices
        };
    }

    public async Task<DashboardProductsResponse> GetProductsAsync(DashboardSummaryFilterRequest request)
    {
        int totalProducts = await _context.Products.CountAsync();
        int activeProducts = await _context.Products.CountAsync(item => item.IsActive);

        return new DashboardProductsResponse
        {
            TotalProducts = totalProducts,
            ActiveProducts = activeProducts
        };
    }

    public async Task<DashboardCustomersResponse> GetCustomersAsync(DashboardSummaryFilterRequest request)
    {
        int totalCustomers = await _context.Customers.CountAsync();
        int activeCustomers = await _context.Customers.CountAsync(item => item.IsActive);

        IQueryable<Domain.Catalog.Entities.Customer> query = _context.Customers.AsNoTracking();
        query = ApplyDateRange(query, request.From, request.To, item => item.CreatedAt);
        int newCustomers = await query.CountAsync();

        return new DashboardCustomersResponse
        {
            TotalCustomers = totalCustomers,
            ActiveCustomers = activeCustomers,
            NewCustomers = newCustomers
        };
    }

    public async Task<DashboardOrdersResponse> GetOrdersAsync(DashboardSummaryFilterRequest request)
    {
        IQueryable<Domain.Sales.Entities.Order> query = BuildOrdersQuery(request)
            .Include(item => item.OrderStatus);

        int totalOrders = await query.CountAsync();
        int pendingOrders = await query.CountAsync(item =>
            item.OrderStatus.Slug == "pending" ||
            item.OrderStatus.Slug == "en-proceso" ||
            item.OrderStatus.Slug == "reservado");
        int deliveredOrders = await query.CountAsync(item =>
            item.OrderStatus.Slug == "delivered" ||
            item.OrderStatus.Slug == "entregado" ||
            item.OrderStatus.Slug == "pagado");

        return new DashboardOrdersResponse
        {
            TotalOrders = totalOrders,
            PendingOrders = pendingOrders,
            DeliveredOrders = deliveredOrders
        };
    }

    public async Task<DashboardInvoicesResponse> GetInvoicesAsync(DashboardSummaryFilterRequest request)
    {
        IQueryable<Domain.Sales.Entities.Invoice> query = _context.Invoices.AsNoTracking();
        query = ApplyDateRange(query, request.From, request.To, item => item.IssuedAt);

        int totalInvoices = await query.CountAsync();
        int pendingInvoices = await query.CountAsync(item =>
            item.Status == "draft" ||
            item.Status == "pending" ||
            item.Status == "processing");
        int acceptedInvoices = await query.CountAsync(item =>
            item.Status == "accepted" ||
            item.Status == "accepted_with_obs" ||
            item.Status == "sent");

        return new DashboardInvoicesResponse
        {
            TotalInvoices = totalInvoices,
            PendingInvoices = pendingInvoices,
            AcceptedInvoices = acceptedInvoices
        };
    }

    public async Task<DashboardAnalyticsSummaryResponse> GetAnalyticsSummaryAsync(DashboardSummaryFilterRequest request)
    {
        var orderRows = await BuildOrdersQuery(request)
            .Include(item => item.OrderStatus)
            .Include(item => item.Warehouse)
            .Include(item => item.Items)
            .ThenInclude(item => item.Product)
            .Select(item => new
            {
                item.Total,
                item.OrderStatus.Slug,
                WarehouseIsPos = item.Warehouse != null && item.Warehouse.IsPos,
                Items = item.Items.Select(orderItem => new
                {
                    orderItem.Quantity,
                    orderItem.Subtotal,
                    UnitCost = orderItem.Product.UnitCost
                }).ToList()
            })
            .ToListAsync();

        int totalOrders = orderRows.Count;
        decimal totalRevenue = orderRows.Sum(item => item.Total);
        int totalUnits = orderRows.Sum(item => item.Items.Sum(orderItem => orderItem.Quantity));
        decimal totalCost = orderRows.Sum(item => item.Items.Sum(orderItem => orderItem.Quantity * orderItem.UnitCost));
        decimal totalProfit = totalRevenue - totalCost;
        decimal avgTicket = totalOrders > 0 ? decimal.Round(totalRevenue / totalOrders, 2) : 0;
        decimal? avgMarginPct = totalRevenue > 0 ? decimal.Round((totalProfit / totalRevenue) * 100, 2) : null;
        int pendingOrders = orderRows.Count(item =>
            item.Slug == "pending" ||
            item.Slug == "en-proceso" ||
            item.Slug == "reservado");
        int posSalesCount = orderRows.Count(item => item.WarehouseIsPos);
        decimal posSalesRevenue = orderRows.Where(item => item.WarehouseIsPos).Sum(item => item.Total);

        IQueryable<Domain.Catalog.Entities.Customer> customerQuery = _context.Customers.AsNoTracking();
        customerQuery = ApplyDateRange(customerQuery, request.From, request.To, item => item.CreatedAt);
        int newCustomers = await customerQuery.CountAsync();

        return new DashboardAnalyticsSummaryResponse
        {
            TotalOrders = totalOrders,
            TotalRevenue = totalRevenue,
            AvgTicket = avgTicket,
            TotalUnits = totalUnits,
            TotalCost = totalCost,
            TotalProfit = totalProfit,
            AvgMarginPct = avgMarginPct,
            PosSalesCount = posSalesCount,
            PosSalesRevenue = posSalesRevenue,
            PendingOrders = pendingOrders,
            NewCustomers = newCustomers
        };
    }

    public async Task<IReadOnlyList<DashboardSalesByDayResponse>> GetSalesByDayAsync(DashboardSummaryFilterRequest request)
    {
        DateTime from = ResolveFromDate(request);
        DateTime to = ResolveToDate(request);

        var rows = await BuildOrdersQuery(request)
            .GroupBy(item => item.OrderDate.Date)
            .Select(group => new
            {
                Date = group.Key,
                Orders = group.Count(),
                Revenue = group.Sum(item => item.Total)
            })
            .ToListAsync();

        Dictionary<DateTime, (int Orders, decimal Revenue)> map = rows.ToDictionary(
            item => item.Date,
            item => (item.Orders, item.Revenue));

        List<DashboardSalesByDayResponse> result = [];
        for (DateTime current = from.Date; current <= to.Date; current = current.AddDays(1))
        {
            map.TryGetValue(current, out var day);
            result.Add(new DashboardSalesByDayResponse
            {
                Date = current.ToString("yyyy-MM-dd"),
                Orders = day.Orders,
                Revenue = day.Revenue
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<DashboardTopProductResponse>> GetTopProductsAsync(DashboardSummaryFilterRequest request)
    {
        var rows = await _context.OrderItems
            .AsNoTracking()
            .Where(item => item.Order.OrderDate >= ResolveFromDate(request) && item.Order.OrderDate < ResolveToDate(request).Date.AddDays(1))
            .Select(item => new
            {
                Description = item.ProductDescription ?? item.Product.Name,
                item.Quantity,
                item.Subtotal
            })
            .ToListAsync();

        return rows
            .GroupBy(item => string.IsNullOrWhiteSpace(item.Description) ? "Producto" : item.Description.Trim())
            .Select(group => new DashboardTopProductResponse
            {
                ProductDescription = group.Key,
                TotalQty = group.Sum(item => item.Quantity),
                TotalRevenue = group.Sum(item => item.Subtotal)
            })
            .OrderByDescending(item => item.TotalRevenue)
            .ThenByDescending(item => item.TotalQty)
            .Take(10)
            .ToList();
    }

    public async Task<IReadOnlyList<DashboardBranchResponse>> GetByBranchAsync(DashboardSummaryFilterRequest request)
    {
        var rows = await BuildOrdersQuery(request)
            .Include(item => item.Warehouse)
            .Select(item => new
            {
                Branch = item.Warehouse != null ? item.Warehouse.Name : "Sin sucursal",
                item.Total
            })
            .ToListAsync();

        return rows
            .GroupBy(item => item.Branch)
            .Select(group => new DashboardBranchResponse
            {
                Branch = group.Key,
                TotalOrders = group.Count(),
                TotalRevenue = group.Sum(item => item.Total)
            })
            .OrderByDescending(item => item.TotalRevenue)
            .ThenBy(item => item.Branch)
            .ToList();
    }

    public async Task<IReadOnlyList<DashboardPaymentMethodBreakdownResponse>> GetByPaymentMethodAsync(DashboardSummaryFilterRequest request)
    {
        var rows = await BuildOrdersQuery(request)
            .Include(item => item.Warehouse)
            .Include(item => item.Invoices)
            .ThenInclude(item => item.PaymentMethod)
            .Select(item => new
            {
                item.Total,
                item.Observations,
                Invoices = item.Invoices.Select(invoice => invoice.PaymentMethod != null ? invoice.PaymentMethod.Name : null).ToList()
            })
            .ToListAsync();

        return rows
            .Select(item => new DashboardPaymentMethodBreakdownResponse
            {
                Method = ResolvePaymentMethod(item.Observations, item.Invoices),
                Total = item.Total
            })
            .GroupBy(item => item.Method)
            .Select(group => new DashboardPaymentMethodBreakdownResponse
            {
                Method = group.Key,
                Total = group.Sum(item => item.Total)
            })
            .OrderByDescending(item => item.Total)
            .ToList();
    }

    public async Task<IReadOnlyList<DashboardSellerResponse>> GetBySellerAsync(DashboardSummaryFilterRequest request)
    {
        var rows = await BuildOrdersQuery(request)
            .Include(item => item.User)
            .Select(item => new
            {
                Seller = item.User == null
                    ? "Sin usuario"
                    : !string.IsNullOrWhiteSpace(item.User.Name)
                        ? item.User.Name
                        : item.User.Email,
                item.Total
            })
            .ToListAsync();

        return rows
            .GroupBy(item => item.Seller)
            .Select(group => new DashboardSellerResponse
            {
                Seller = group.Key,
                TotalOrders = group.Count(),
                TotalRevenue = group.Sum(item => item.Total),
                AvgTicket = group.Any() ? decimal.Round(group.Average(item => item.Total), 2) : 0
            })
            .OrderByDescending(item => item.TotalRevenue)
            .ToList();
    }

    private IQueryable<Domain.Sales.Entities.Order> BuildOrdersQuery(DashboardSummaryFilterRequest request)
    {
        IQueryable<Domain.Sales.Entities.Order> query = _context.Orders.AsNoTracking();
        query = ApplyDateRange(query, request.From, request.To, item => item.OrderDate);
        return query;
    }

    private static IQueryable<TEntity> ApplyDateRange<TEntity>(
        IQueryable<TEntity> query,
        DateTime? from,
        DateTime? to,
        Expression<Func<TEntity, DateTime>> selector)
        where TEntity : class
    {
        if (from.HasValue)
        {
            DateTime fromDate = from.Value.Date;
            query = query.Where(BuildGreaterThanOrEqual(selector, fromDate));
        }

        if (to.HasValue)
        {
            DateTime toDateExclusive = to.Value.Date.AddDays(1);
            query = query.Where(BuildLessThan(selector, toDateExclusive));
        }

        return query;
    }

    private static Expression<Func<TEntity, bool>> BuildGreaterThanOrEqual<TEntity>(
        Expression<Func<TEntity, DateTime>> selector,
        DateTime value)
    {
        var parameter = selector.Parameters[0];
        var body = Expression.GreaterThanOrEqual(selector.Body, Expression.Constant(value));
        return Expression.Lambda<Func<TEntity, bool>>(body, parameter);
    }

    private static Expression<Func<TEntity, bool>> BuildLessThan<TEntity>(
        Expression<Func<TEntity, DateTime>> selector,
        DateTime value)
    {
        var parameter = selector.Parameters[0];
        var body = Expression.LessThan(selector.Body, Expression.Constant(value));
        return Expression.Lambda<Func<TEntity, bool>>(body, parameter);
    }

    private static DateTime ResolveFromDate(DashboardSummaryFilterRequest request)
        => request.From?.Date ?? DateTime.Today.AddDays(-29);

    private static DateTime ResolveToDate(DashboardSummaryFilterRequest request)
        => request.To?.Date ?? DateTime.Today;

    private static string ResolvePaymentMethod(string? observations, IEnumerable<string?> invoiceMethods)
    {
        string? invoiceMethod = invoiceMethods.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
        if (!string.IsNullOrWhiteSpace(invoiceMethod))
        {
            return invoiceMethod.Trim();
        }

        if (!string.IsNullOrWhiteSpace(observations))
        {
            Match match = PosPaymentMethodRegex.Match(observations);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }

        return "Sin metodo";
    }
}
