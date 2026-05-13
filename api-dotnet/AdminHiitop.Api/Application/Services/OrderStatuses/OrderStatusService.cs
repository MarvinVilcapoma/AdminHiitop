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

    public async Task<object> GetAsync(int perPage, int page, CancellationToken cancellationToken)
        => await PaginationHelper.CreateAsync(
            _context.OrderStatuses.AsNoTracking().OrderBy(item => item.SortOrder).ThenBy(item => item.Name),
            page, perPage, cancellationToken);

    public Task<OrderStatus?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => _context.OrderStatuses.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<OrderStatus> CreateAsync(OrderStatus request, CancellationToken cancellationToken)
    {
        _context.OrderStatuses.Add(request);
        await _context.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<OrderStatus> UpdateAsync(int id, OrderStatus request, CancellationToken cancellationToken)
    {
        OrderStatus entity = await FindAsync(id, cancellationToken);
        entity.Name = string.IsNullOrWhiteSpace(request.Name) ? entity.Name : request.Name;
        entity.Slug = string.IsNullOrWhiteSpace(request.Slug) ? entity.Slug : request.Slug;
        entity.Color = request.Color ?? entity.Color;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.IsProtected = request.IsProtected;
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        OrderStatus entity = await FindAsync(id, cancellationToken);
        _context.OrderStatuses.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<OrderStatus> FindAsync(int id, CancellationToken cancellationToken)
    {
        OrderStatus? entity = await _context.OrderStatuses.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) throw new AppException("Estado de pedido no encontrado.", 404);
        return entity;
    }
}
