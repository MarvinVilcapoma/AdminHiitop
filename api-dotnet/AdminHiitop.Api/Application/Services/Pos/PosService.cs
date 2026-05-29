using AdminHiitop.Api.Application.DTOs.Pos;
using AdminHiitop.Api.Application.DTOs.Orders;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Pos;

public sealed class PosService : IPosService
{
    private readonly AdminHiitopDbContext _context;
    private readonly IOrderService _orderService;

    // DocumentType.Code (display) → SUNAT numeric code used in InvoiceSeries.DocType
    private static readonly Dictionary<string, string> SunatCodeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BOLETA"]               = "03",
        ["FACTURA"]              = "01",
        ["NOTA_CREDITO"]         = "07",
        ["NOTA_DEBITO"]          = "08",
        ["GUIA_REMISION"]        = "09",
        ["GUIA_REMISION_TRANSP"] = "31",
    };

    public PosService(AdminHiitopDbContext context, IOrderService orderService)
    {
        _context = context;
        _orderService = orderService;
    }

    public async Task<PosInitialDataResponse> GetInitialDataAsync()
    {
        List<Warehouse> warehouses = await _context.Warehouses
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.Name)
            .ToListAsync();

        List<DocumentType> documentTypes = await _context.DocumentTypes
            .AsNoTracking()
            .Where(item => item.IsActive)
            .Include(item => item.DocumentTypePrintFormats)
            .ThenInclude(item => item.DocumentPrintFormat)
            .OrderBy(item => item.SortOrder)
            .ToListAsync();

        List<DocumentPrintFormat> activePrintFormats = await _context.DocumentPrintFormats
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.Name)
            .ToListAsync();

        // Map SUNAT numeric code → first active invoice series ID (GroupBy avoids duplicate-key when
        // multiple series share the same DocType, e.g. FC01 and BC01 both use "07").
        Dictionary<string, int> seriesIdBySunatCode = (await _context.InvoiceSeries
            .AsNoTracking()
            .Where(s => s.IsActive)
            .ToListAsync())
            .GroupBy(s => s.DocType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        List<PaymentMethod> paymentMethods = await _context.PaymentMethods
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.Name)
            .ToListAsync();

        List<Color> colors = await _context.Colors
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .ToListAsync();

        Dictionary<string, PosSettingResponse> settings = await _context.Settings
            .AsNoTracking()
            .ToDictionaryAsync(
                item => item.Key,
                item => new PosSettingResponse
                {
                    Value = item.Value,
                    Label = item.Label,
                    Type = item.Type,
                    Group = item.Group
                });

        return new PosInitialDataResponse
        {
            Warehouses = warehouses.Select(item => new PosWarehouseResponse
            {
                Id = item.Id,
                Name = item.Name,
                Code = item.Code,
                City = item.City,
                IsActive = item.IsActive,
                IsPos = item.IsPos
            }).ToList(),
            DocumentTypes = documentTypes.Select(item => new PosDocumentTypeResponse
            {
                Id = item.Id,
                Code = item.Code,
                Name = item.Name,
                IsActive = item.IsActive,
                IsProtected = item.IsProtected,
                IsSunatDocument = item.IsSunatDocument,
                RequiresCustomer = item.RequiresCustomer,
                RequiresRelatedDocument = item.RequiresRelatedDocument,
                CanBeConverted = item.CanBeConverted,
                IsCommercialDocument = item.IsCommercialDocument,
                SortOrder = item.SortOrder,
                PrintFormats = ResolvePrintFormats(item, activePrintFormats),
                InvoiceSeriesId = item.IsSunatDocument
                    && SunatCodeMap.TryGetValue(item.Code ?? "", out string? sunatCode)
                    && seriesIdBySunatCode.TryGetValue(sunatCode, out int sid)
                        ? sid : null
            }).ToList(),
            PaymentMethods = paymentMethods.Select(item => new PosPaymentMethodResponse
            {
                Id = item.Id,
                Name = item.Name,
                Code = item.Code,
                IsActive = item.IsActive
            }).ToList(),
            Colors = colors.Select(item => new PosColorResponse
            {
                Id = item.Id,
                Name = item.Name,
                HexCode = item.HexCode,
                Slug = item.Slug
            }).ToList(),
            Settings = settings
        };
    }

    public async Task<Order> CreateOrderAsync(PosOrderCreateRequest request, int? userId = null)
    {
        if (request.WarehouseId <= 0)
        {
            throw new AppException("El almacén POS es obligatorio.", 400);
        }

        if (request.PaymentMethodId <= 0)
        {
            throw new AppException("El método de pago es obligatorio.", 400);
        }

        if (request.DocumentTypeId <= 0)
        {
            throw new AppException("El tipo de documento es obligatorio.", 400);
        }

        Warehouse warehouse = await _context.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.WarehouseId && item.IsActive)
            ?? throw new AppException("El almacén seleccionado no existe o está inactivo.", 404);

        if (!warehouse.IsPos)
        {
            throw new AppException("El almacén seleccionado no está habilitado como punto de venta.", 422);
        }

        bool paymentMethodExists = await _context.PaymentMethods
            .AsNoTracking()
            .AnyAsync(item => item.Id == request.PaymentMethodId && item.IsActive);

        if (!paymentMethodExists)
        {
            throw new AppException("El método de pago seleccionado no existe o está inactivo.", 404);
        }

        bool documentTypeExists = await _context.DocumentTypes
            .AsNoTracking()
            .AnyAsync(item => item.Id == request.DocumentTypeId && item.IsActive);

        if (!documentTypeExists)
        {
            throw new AppException("El tipo de documento seleccionado no existe o está inactivo.", 404);
        }

        int resolvedStatusId = request.OrderStatusId.GetValueOrDefault();
        if (resolvedStatusId <= 0)
        {
            resolvedStatusId = await ResolveDefaultPosStatusIdAsync();
        }

        string paymentMethodName = await _context.PaymentMethods
            .AsNoTracking()
            .Where(item => item.Id == request.PaymentMethodId)
            .Select(item => item.Name)
            .FirstAsync();

        string? mergedObservations = string.Join(" | ", new[]
        {
            $"POS · Metodo de pago: {paymentMethodName}",
            request.Observations?.Trim()
        }.Where(item => !string.IsNullOrWhiteSpace(item)));

        OrderUpsertRequest orderRequest = new()
        {
            OrderDate = request.OrderDate == default ? PeruClock.Now : request.OrderDate,
            OrderStatusId = resolvedStatusId,
            WarehouseId = request.WarehouseId,
            Observations = mergedObservations,
            Phone = request.Phone,
            CustomerId = request.CustomerId,
            CustomerName = request.CustomerName,
            Dni = request.CustomerDocument,
            Address = request.Address,
            PickupKey = null,
            TrackingNumber = null,
            DeliveryCost = 0,
            Total = request.Total,
            DocumentTypeId = request.DocumentTypeId,
            DocumentPrintFormatId = request.DocumentPrintFormatId,
            CustomerEmail = request.CustomerEmail,
            NeedsReceipt = false,
            UserId = userId ?? request.UserId,
            Items = request.Items.Select(item => new OrderItemUpsertRequest
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
            }).ToList()
        };

        return await _orderService.CreateAsync(orderRequest);
    }

    private async Task<int> ResolveDefaultPosStatusIdAsync()
    {
        string[] preferredSlugs = ["delivered", "entregado", "pagado", "pending"];

        foreach (string slug in preferredSlugs)
        {
            int? statusId = await _context.OrderStatuses
                .AsNoTracking()
                .Where(item => item.Slug == slug && item.IsActive)
                .Select(item => (int?)item.Id)
                .FirstOrDefaultAsync();

            if (statusId.HasValue)
            {
                return statusId.Value;
            }
        }

        int? fallbackStatusId = await _context.OrderStatuses
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Select(item => (int?)item.Id)
            .FirstOrDefaultAsync();

        if (!fallbackStatusId.HasValue)
        {
            throw new AppException("No hay estados de pedido configurados para registrar ventas POS.", 500);
        }

        return fallbackStatusId.Value;
    }

    private static IReadOnlyList<PosPrintFormatResponse> ResolvePrintFormats(
        DocumentType documentType,
        IReadOnlyList<DocumentPrintFormat> activePrintFormats)
    {
        string documentCode = documentType.Code?.ToUpperInvariant() ?? string.Empty;

        List<PosPrintFormatResponse> linkedFormats = documentType.DocumentTypePrintFormats
            .Where(link => link.DocumentPrintFormat.IsActive)
            .OrderByDescending(link => link.IsDefault)
            .ThenBy(link => link.DocumentPrintFormat.Name)
            .Select(link => new PosPrintFormatResponse
            {
                Id = link.DocumentPrintFormat.Id,
                Code = link.DocumentPrintFormat.Code,
                Name = link.DocumentPrintFormat.Name,
                Mode = link.DocumentPrintFormat.Mode,
                WidthMm = link.DocumentPrintFormat.WidthMm,
                IsActive = link.DocumentPrintFormat.IsActive,
                Pivot = new PosPrintFormatPivotResponse
                {
                    IsDefault = link.IsDefault
                }
            })
            .ToList();

        if (linkedFormats.Count > 0)
        {
            bool hasDefault = documentType.DocumentTypePrintFormats.Any(link => link.DocumentPrintFormat.IsActive && link.IsDefault);
            if (hasDefault)
            {
                return linkedFormats;
            }

            return linkedFormats
                .Select((format, index) => new PosPrintFormatResponse
                {
                    Id = format.Id,
                    Code = format.Code,
                    Name = format.Name,
                    Mode = format.Mode,
                    WidthMm = format.WidthMm,
                    IsActive = format.IsActive,
                    Pivot = new PosPrintFormatPivotResponse
                    {
                        IsDefault = index == 0
                    }
                })
                .ToList();
        }

        DocumentPrintFormat? pdfFormat = activePrintFormats.FirstOrDefault(item => item.Code == "PDF");
        DocumentPrintFormat? ticketFormat = activePrintFormats.FirstOrDefault(item => item.Code == "TICKET");
        DocumentPrintFormat? a4Format = activePrintFormats.FirstOrDefault(item => item.Code == "A4");

        List<(DocumentPrintFormat Format, bool IsDefault)> fallbackFormats = new();

        if (documentCode is "TICKET" or "COTIZACION" or "ORDEN_VENTA")
        {
            // Internal documents — thermal ticket is natural default
            if (ticketFormat is not null) fallbackFormats.Add((ticketFormat, true));
            if (pdfFormat    is not null) fallbackFormats.Add((pdfFormat,    false));
            if (a4Format     is not null) fallbackFormats.Add((a4Format,     false));
        }
        else if (documentCode is "BOLETA" or "FACTURA")
        {
            // Point-of-sale tax documents — TICKET default for quick thermal print
            if (ticketFormat is not null) fallbackFormats.Add((ticketFormat, true));
            if (pdfFormat    is not null) fallbackFormats.Add((pdfFormat,    false));
            if (a4Format     is not null) fallbackFormats.Add((a4Format,     false));
        }
        else if (documentCode is "NOTA_CREDITO" or "NOTA_DEBITO")
        {
            // Credit/debit notes — formal documents, A4 is standard
            if (a4Format     is not null) fallbackFormats.Add((a4Format,     true));
            if (pdfFormat    is not null) fallbackFormats.Add((pdfFormat,    false));
            if (ticketFormat is not null) fallbackFormats.Add((ticketFormat, false));
        }
        else if (documentCode is "GUIA_REMISION" or "GUIA_REMISION_TRANSP")
        {
            // Transport guides — A4 for the physical document
            if (a4Format     is not null) fallbackFormats.Add((a4Format,     true));
            if (pdfFormat    is not null) fallbackFormats.Add((pdfFormat,    false));
        }
        else
        {
            DocumentPrintFormat? defaultFormat = pdfFormat ?? ticketFormat ?? a4Format ?? activePrintFormats.FirstOrDefault();
            if (defaultFormat is not null)
            {
                fallbackFormats.Add((defaultFormat, true));
            }
        }

        return fallbackFormats
            .DistinctBy(item => item.Format.Id)
            .Select(item => new PosPrintFormatResponse
            {
                Id = item.Format.Id,
                Code = item.Format.Code,
                Name = item.Format.Name,
                Mode = item.Format.Mode,
                WidthMm = item.Format.WidthMm,
                IsActive = item.Format.IsActive,
                Pivot = new PosPrintFormatPivotResponse
                {
                    IsDefault = item.IsDefault
                }
            })
            .ToList();
    }
}
