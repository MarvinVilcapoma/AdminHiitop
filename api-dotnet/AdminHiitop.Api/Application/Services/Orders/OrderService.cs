using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Orders;

public sealed class OrderService : IOrderService
{
    private readonly IOrderQueryService _orderQueryService;
    private readonly AdminHiitopDbContext _context;

    public OrderService(IOrderQueryService orderQueryService, AdminHiitopDbContext context)
    {
        _orderQueryService = orderQueryService;
        _context = context;
    }

    public async Task<object> GetAsync(string? search, int? perPage, int page, bool withSummary, int? orderStatusId)
    {
        if (!perPage.HasValue)
        {
            return await _orderQueryService.GetAsync(search);
        }

        IQueryable<Order> query = _context.Orders
            .AsNoTracking()
            .Include(item => item.OrderStatus)
            .Include(item => item.DocumentType)
            .Include(item => item.DocumentPrintFormat)
            .Include(item => item.Customer)
            .Include(item => item.Invoices)
            .OrderByDescending(item => item.OrderDate)
            .ThenByDescending(item => item.Id);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(item => item.OrderNumber.Contains(term) || (item.CustomerName != null && item.CustomerName.Contains(term)));
        }

        if (orderStatusId.HasValue)
        {
            query = query.Where(item => item.OrderStatusId == orderStatusId.Value);
        }

        var paged = await PaginationHelper.CreateAsync(query, page, perPage.Value);
        if (!withSummary)
        {
            return paged;
        }

        return new
        {
            paged.Data,
            paged.CurrentPage,
            paged.LastPage,
            paged.PerPage,
            paged.Total,
            summary = new
            {
                total_orders = await _context.Orders.CountAsync(),
                pending_shipping = await _context.Orders.CountAsync(item => item.OrderStatus.Slug == "pending"),
                total_revenue = await _context.Orders.SumAsync(item => item.Total)
            }
        };
    }

    public Task<Order?> GetByIdAsync(int id)
    {
        return _context.Orders
            .AsNoTracking()
            .Include(item => item.OrderStatus)
            .Include(item => item.Customer)
            .Include(item => item.Items)
            .Include(item => item.Invoices)
            .Include(item => item.DocumentType)
            .FirstOrDefaultAsync(item => item.Id == id);
    }

    public async Task<Order> CreateAsync(Order request)
    {
        if (string.IsNullOrWhiteSpace(request.OrderNumber))
        {
            request.OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}";
        }

        _context.Orders.Add(request);
        await _context.SaveChangesAsync();
        return request;
    }

    public async Task<Order> UpdateAsync(int id, Order request)
    {
        Order entity = await FindAsync(id);
        entity.OrderStatusId = request.OrderStatusId == 0 ? entity.OrderStatusId : request.OrderStatusId;
        entity.CustomerName = request.CustomerName ?? entity.CustomerName;
        entity.Phone = request.Phone ?? entity.Phone;
        entity.Address = request.Address ?? entity.Address;
        entity.CustomerEmail = request.CustomerEmail ?? entity.CustomerEmail;
        entity.Observations = request.Observations ?? entity.Observations;
        entity.Total = request.Total == 0 ? entity.Total : request.Total;
        entity.DeliveryCost = request.DeliveryCost;
        entity.NeedsReceipt = request.NeedsReceipt;
        entity.DocumentTypeId = request.DocumentTypeId;
        entity.DocumentPrintFormatId = request.DocumentPrintFormatId;
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task DeleteAsync(int id)
    {
        Order entity = await FindAsync(id);
        _context.Orders.Remove(entity);
        await _context.SaveChangesAsync();
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
}
