using AdminHiitop.Api.Application.DTOs.Catalogs;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Catalogs;

public sealed class CatalogQueryService : ICatalogQueryService
{
    private readonly AdminHiitopDbContext _context;

    public CatalogQueryService(AdminHiitopDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CatalogItemResponse>> GetDocumentTypesAsync() =>
        await _context.DocumentTypes
            .AsNoTracking()
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Select(item => new CatalogItemResponse
            {
                Id = item.Id,
                Code = item.Code,
                Name = item.Name,
                IsActive = item.IsActive
            })
            .ToListAsync();

    public async Task<IReadOnlyList<CatalogItemResponse>> GetWarehousesAsync() =>
        await _context.Warehouses
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .Select(item => new CatalogItemResponse
            {
                Id = item.Id,
                Code = item.Code,
                Name = item.Name,
                IsActive = item.IsActive
            })
            .ToListAsync();

    public async Task<IReadOnlyList<CatalogItemResponse>> GetPaymentMethodsAsync() =>
        await _context.PaymentMethods
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .Select(item => new CatalogItemResponse
            {
                Id = item.Id,
                Code = item.Code,
                Name = item.Name,
                IsActive = item.IsActive
            })
            .ToListAsync();
}
