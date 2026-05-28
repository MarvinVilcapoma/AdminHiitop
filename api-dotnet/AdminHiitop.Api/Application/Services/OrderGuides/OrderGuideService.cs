using AdminHiitop.Api.Application.DTOs.Common;
using AdminHiitop.Api.Application.DTOs.ElectronicBilling;
using AdminHiitop.Api.Application.Helpers;
using AdminHiitop.Api.Application.Interfaces.ElectronicBilling;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.OrderGuides;

public sealed class OrderGuideService(
    AdminHiitopDbContext context,
    IElectronicBillingProvider billingProvider,
    IInvoiceSeriesService seriesService) : IOrderGuideService
{
    private readonly AdminHiitopDbContext _context = context;
    private readonly IElectronicBillingProvider _billingProvider = billingProvider;
    private readonly IInvoiceSeriesService _seriesService = seriesService;

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
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(item => item.Id == orderId);
    }

    public async Task<object?> SendAsync(int orderId)
    {
        Order? order = await _context.Orders
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(item => item.Id == orderId);

        if (order is null) return null;

        // Atomic increment from centralized series table (DocType 09 = Guía de Remisión)
        (string serie, int correlativo) = await _seriesService.GetNextAsync("09", "T001");

        order.GuideSeries      = serie;
        order.GuideCorrelativo = correlativo;
        order.GuideFullNumber  = $"{serie}-{correlativo:00000000}";
        order.GuideSentAt      = PeruClock.Now;

        NubeFactGuideDocumentRequest guideRequest = NubeFactMappingHelper.MapGuide(order, serie, correlativo);
        NubeFactSubmitResult result = await _billingProvider.SendGuideDocumentAsync(guideRequest);

        order.GuideStatus           = result.Success ? "accepted" : "error";
        order.GuideSunatCode        = int.TryParse(result.Response.SunatResponseCode, out int code) ? code : null;
        order.GuideSunatDescription = result.Response.SunatDescription ?? result.Response.Errors;
        order.GuideXmlContent       = result.Response.XmlZipBase64;
        order.GuideCdrContent       = result.Response.CdrZipBase64;

        await _context.SaveChangesAsync();

        return new
        {
            success     = result.Success,
            provider    = result.ProviderName,
            environment = result.Environment,
            order = new
            {
                order.Id,
                order.GuideFullNumber,
                order.GuideStatus,
                order.GuideSunatDescription,
            },
            result = result.Response,
        };
    }

    public async Task<FileDownloadResponse?> GetXmlAsync(int orderId)
    {
        Order? order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == orderId);

        if (order is null || string.IsNullOrWhiteSpace(order.GuideXmlContent))
            return null;

        byte[]? content = NubeFactStorageHelper.DecodeBase64(order.GuideXmlContent);
        if (content is null) return null;

        return new FileDownloadResponse
        {
            Content     = content,
            ContentType = "application/octet-stream",
            FileName    = $"{order.GuideFullNumber}.xml.zip"
        };
    }

    public async Task<FileDownloadResponse?> GetCdrAsync(int orderId)
    {
        Order? order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == orderId);

        if (order is null || string.IsNullOrWhiteSpace(order.GuideCdrContent))
            return null;

        byte[]? content = NubeFactStorageHelper.DecodeBase64(order.GuideCdrContent);
        if (content is null) return null;

        return new FileDownloadResponse
        {
            Content     = content,
            ContentType = "application/octet-stream",
            FileName    = $"{order.GuideFullNumber}.cdr.zip"
        };
    }
}
