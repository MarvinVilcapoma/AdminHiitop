using AdminHiitop.Api.Application.DTOs.Pos;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Pos;

public sealed class PosService : IPosService
{
    private readonly AdminHiitopDbContext _context;

    public PosService(AdminHiitopDbContext context)
    {
        _context = context;
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
                PrintFormats = ResolvePrintFormats(item, activePrintFormats)
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

    private static IReadOnlyList<PosPrintFormatResponse> ResolvePrintFormats(
        DocumentType documentType,
        IReadOnlyList<DocumentPrintFormat> activePrintFormats)
    {
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

        DocumentPrintFormat? pdfFormat = activePrintFormats.FirstOrDefault(item => item.Code == "PDF")
            ?? activePrintFormats.FirstOrDefault();

        if (pdfFormat is null)
        {
            return [];
        }

        return
        [
            new PosPrintFormatResponse
            {
                Id = pdfFormat.Id,
                Code = pdfFormat.Code,
                Name = pdfFormat.Name,
                Mode = pdfFormat.Mode,
                WidthMm = pdfFormat.WidthMm,
                IsActive = pdfFormat.IsActive,
                Pivot = new PosPrintFormatPivotResponse
                {
                    IsDefault = true
                }
            }
        ];
    }
}
