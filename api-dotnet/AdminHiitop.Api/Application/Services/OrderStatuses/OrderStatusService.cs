using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.OrderStatuses;

public sealed class OrderStatusService : IOrderStatusService
{
    private readonly AdminHiitopDbContext _context;

    public OrderStatusService(AdminHiitopDbContext context) => _context = context;

    public async Task<object> GetAsync(int perPage, int page)
        => await PaginationHelper.CreateAsync(
            _context.OrderStatuses.AsNoTracking().OrderBy(item => item.SortOrder).ThenBy(item => item.Name),
            page, perPage);

    public Task<OrderStatus?> GetByIdAsync(int id)
        => _context.OrderStatuses.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);

    public async Task<OrderStatus> CreateAsync(OrderStatus request)
    {
        _context.OrderStatuses.Add(request);
        await _context.SaveChangesAsync();
        return request;
    }

    public async Task<OrderStatus> UpdateAsync(int id, OrderStatus request)
    {
        OrderStatus entity = await FindAsync(id);
        entity.Name = string.IsNullOrWhiteSpace(request.Name) ? entity.Name : request.Name;
        entity.Slug = string.IsNullOrWhiteSpace(request.Slug) ? entity.Slug : request.Slug;
        entity.Color = request.Color ?? entity.Color;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.IsProtected = request.IsProtected;
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task DeleteAsync(int id)
    {
        OrderStatus entity = await FindAsync(id);
        _context.OrderStatuses.Remove(entity);
        await _context.SaveChangesAsync();
    }

    private async Task<OrderStatus> FindAsync(int id)
    {
        OrderStatus? entity = await _context.OrderStatuses.FirstOrDefaultAsync(item => item.Id == id);
        if (entity is null) throw new AppException("Estado de pedido no encontrado.", 404);
        return entity;
    }
}
