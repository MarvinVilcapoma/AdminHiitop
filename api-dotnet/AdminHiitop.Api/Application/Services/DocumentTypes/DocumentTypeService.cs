using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Helpers;
using AdminHiitop.Api.Shared.Models;
using AdminHiitop.Api.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.DocumentTypes;

public sealed class DocumentTypeService : IDocumentTypeService
{
    private readonly ICatalogQueryService _catalogQueryService;
    private readonly AdminHiitopDbContext _context;

    public DocumentTypeService(ICatalogQueryService catalogQueryService, AdminHiitopDbContext context)
    {
        _catalogQueryService = catalogQueryService;
        _context = context;
    }

    public async Task<object> GetAsync(int? perPage, int page, string? search, bool activeOnly = false)
    {
        if (!perPage.HasValue)
        {
            return await _catalogQueryService.GetDocumentTypesAsync();
        }

        IQueryable<DocumentType> query = _context.DocumentTypes
            .AsNoTracking()
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(item => item.Name.Contains(term) || item.Code.Contains(term));
        }

        if (activeOnly)
            query = query.Where(item => item.IsActive);

        return await PaginationHelper.CreateAsync(query, page, perPage.Value);
    }

    public async Task<object> GetByIdAsync(int id)
    {
        DocumentType entity = await FindForReadAsync(id);

        return new
        {
            entity.Id,
            entity.Code,
            entity.Name,
            entity.IsActive,
            entity.IsProtected,
            entity.IsSunatDocument,
            entity.RequiresCustomer,
            entity.RequiresRelatedDocument,
            entity.CanBeConverted,
            entity.IsCommercialDocument,
            entity.SortOrder,
            print_formats = entity.DocumentTypePrintFormats
                .Select(item => new
                {
                    item.DocumentPrintFormat.Id,
                    item.DocumentPrintFormat.Code,
                    item.DocumentPrintFormat.Name,
                    item.DocumentPrintFormat.Mode,
                    item.DocumentPrintFormat.WidthMm,
                    item.DocumentPrintFormat.IsActive,
                    pivot = new { is_default = item.IsDefault }
                })
                .ToList()
        };
    }

    public async Task<DocumentType> CreateAsync(DocumentType request)
    {
        _context.DocumentTypes.Add(request);
        await _context.SaveChangesAsync();
        return request;
    }

    public async Task<DocumentType> UpdateAsync(int id, DocumentType request)
    {
        DocumentType entity = await FindAsync(id);
        entity.Name = string.IsNullOrWhiteSpace(request.Name) ? entity.Name : request.Name;
        entity.Code = string.IsNullOrWhiteSpace(request.Code) ? entity.Code : request.Code;
        entity.IsActive = request.IsActive;
        entity.IsProtected = request.IsProtected;
        entity.IsSunatDocument = request.IsSunatDocument;
        entity.RequiresCustomer = request.RequiresCustomer;
        entity.RequiresRelatedDocument = request.RequiresRelatedDocument;
        entity.CanBeConverted = request.CanBeConverted;
        entity.IsCommercialDocument = request.IsCommercialDocument;
        entity.SortOrder = request.SortOrder;
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task DeleteAsync(int id)
    {
        DocumentType entity = await FindAsync(id);
        _context.DocumentTypes.Remove(entity);
        await _context.SaveChangesAsync();
    }

    private async Task<DocumentType> FindAsync(int id)
    {
        DocumentType? entity = await _context.DocumentTypes.FirstOrDefaultAsync(item => item.Id == id);
        if (entity is null)
        {
            throw new AppException("Tipo de documento no encontrado.", 404);
        }

        return entity;
    }

    private async Task<DocumentType> FindForReadAsync(int id)
    {
        DocumentType? entity = await _context.DocumentTypes
            .AsNoTracking()
            .Include(item => item.DocumentTypePrintFormats)
            .ThenInclude(item => item.DocumentPrintFormat)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (entity is null)
        {
            throw new AppException("Tipo de documento no encontrado.", 404);
        }

        return entity;
    }
}
