using AdminHiitop.Api.Application.DTOs.Dashboard;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Dashboard;

public sealed class DashboardQueryService : IDashboardQueryService
{
    private readonly AdminHiitopDbContext _context;

    public DashboardQueryService(AdminHiitopDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardSummaryResponse> GetSummaryAsync(DashboardSummaryFilterRequest request)
    {
        int totalProducts = await _context.Products.CountAsync();
        int activeProducts = await _context.Products.CountAsync(item => item.IsActive);
        int totalCustomers = await _context.Customers.CountAsync();
        int activeCustomers = await _context.Customers.CountAsync(item => item.IsActive);
        int totalOrders = await _context.Orders.CountAsync();
        int pendingOrders = await _context.Orders.CountAsync(
            item => item.OrderStatus.Slug == "pending" || item.OrderStatus.Slug == "en-proceso");
        int totalInvoices = await _context.Invoices.CountAsync();
        int pendingInvoices = await _context.Invoices.CountAsync(
            item => item.Status == "draft" || item.Status == "pending" || item.Status == "processing");

        return new DashboardSummaryResponse
        {
            TotalProducts = totalProducts,
            ActiveProducts = activeProducts,
            TotalCustomers = totalCustomers,
            ActiveCustomers = activeCustomers,
            TotalOrders = totalOrders,
            PendingOrders = pendingOrders,
            TotalInvoices = totalInvoices,
            PendingInvoices = pendingInvoices
        };
    }
}
