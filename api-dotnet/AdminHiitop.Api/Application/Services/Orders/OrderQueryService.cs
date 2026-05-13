using AdminHiitop.Api.Application.DTOs.Orders;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Orders;

public sealed class OrderQueryService : IOrderQueryService
{
    private readonly AdminHiitopDbContext _context;

    public OrderQueryService(AdminHiitopDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<OrderListItemResponse>> GetAsync(string? search)
    {
        string normalizedSearch = NormalizeSearch(search);
        IQueryable<Domain.Sales.Entities.Order> query = _context.Orders
            .AsNoTracking()
            .Include(item => item.OrderStatus);

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(item =>
                item.OrderNumber.Contains(normalizedSearch) ||
                (item.CustomerName != null && item.CustomerName.Contains(normalizedSearch)) ||
                (item.DocumentNumber != null && item.DocumentNumber.Contains(normalizedSearch)));
        }

        return await query
            .OrderByDescending(item => item.OrderDate)
            .ThenByDescending(item => item.Id)
            .Select(item => new OrderListItemResponse
            {
                Id = item.Id,
                OrderNumber = item.OrderNumber,
                OrderDate = item.OrderDate,
                StatusName = item.OrderStatus.Name,
                CustomerName = item.CustomerName,
                Total = item.Total,
                NeedsReceipt = item.NeedsReceipt
            })
            .ToListAsync();
    }

    private static string NormalizeSearch(string? search) =>
        string.IsNullOrWhiteSpace(search) ? string.Empty : search.Trim();
}
