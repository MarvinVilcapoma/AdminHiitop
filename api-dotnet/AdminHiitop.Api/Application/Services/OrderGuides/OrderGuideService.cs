using System.Text;
using AdminHiitop.Api.Application.DTOs.Common;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.OrderGuides;

public sealed class OrderGuideService : IOrderGuideService
{
    private readonly AdminHiitopDbContext _context;

    public OrderGuideService(AdminHiitopDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Order>> GetGuidesAsync()
    {
        return await _context.Orders
            .AsNoTracking()
            .Where(item => item.GuideSeries != null || item.GuideStatus != null)
            .OrderByDescending(item => item.OrderDate)
            .ToListAsync();
    }

    public Task<Order?> GetByOrderIdAsync(int orderId)
    {
        return _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == orderId);
    }

    public async Task<object?> SendAsync(int orderId)
    {
        Order? order = await _context.Orders.FirstOrDefaultAsync(item => item.Id == orderId);
        if (order is null)
        {
            return null;
        }

        order.GuideSeries ??= "T001";
        order.GuideCorrelativo ??= 1;
        order.GuideFullNumber = $"{order.GuideSeries}-{order.GuideCorrelativo:00000000}";
        order.GuideStatus = "accepted";
        order.GuideSunatDescription = "Guia enviada correctamente.";
        order.GuideXmlContent = "<xml>guide</xml>";
        order.GuideCdrContent = "cdr-guide";
        order.GuideSentAt = PeruClock.Now;

        await _context.SaveChangesAsync();

        return new
        {
            success = true,
            order,
            result = new
            {
                description = order.GuideSunatDescription
            }
        };
    }

    public async Task<FileDownloadResponse?> GetXmlAsync(int orderId)
    {
        Order? order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == orderId);

        if (order is null || string.IsNullOrWhiteSpace(order.GuideXmlContent))
        {
            return null;
        }

        return new FileDownloadResponse
        {
            Content = Encoding.UTF8.GetBytes(order.GuideXmlContent),
            ContentType = "application/xml",
            FileName = $"{order.GuideFullNumber}.xml"
        };
    }

    public async Task<FileDownloadResponse?> GetCdrAsync(int orderId)
    {
        Order? order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == orderId);

        if (order is null || string.IsNullOrWhiteSpace(order.GuideCdrContent))
        {
            return null;
        }

        return new FileDownloadResponse
        {
            Content = Encoding.UTF8.GetBytes(order.GuideCdrContent),
            ContentType = "text/plain",
            FileName = $"{order.GuideFullNumber}.cdr.txt"
        };
    }
}
