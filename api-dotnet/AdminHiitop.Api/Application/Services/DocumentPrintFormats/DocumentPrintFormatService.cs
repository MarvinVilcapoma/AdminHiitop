using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.DocumentPrintFormats;

public sealed class DocumentPrintFormatService : IDocumentPrintFormatService
{
    private readonly AdminHiitopDbContext _context;

    public DocumentPrintFormatService(AdminHiitopDbContext context)
    {
        _context = context;
    }

    public async Task<object> GetAsync(int perPage, int page, string? search)
    {
        IQueryable<DocumentPrintFormat> query = _context.DocumentPrintFormats
            .AsNoTracking()
            .OrderBy(item => item.Name);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(item => item.Name.Contains(term) || item.Code.Contains(term));
        }

        return await PaginationHelper.CreateAsync(query, page, perPage);
    }

    public Task<DocumentPrintFormat?> GetByIdAsync(int id)
    {
        return _context.DocumentPrintFormats
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id);
    }

    public async Task<DocumentPrintFormat> CreateAsync(DocumentPrintFormat request)
    {
        _context.DocumentPrintFormats.Add(request);
        await _context.SaveChangesAsync();
        return request;
    }

    public async Task<DocumentPrintFormat> UpdateAsync(int id, DocumentPrintFormat request)
    {
        DocumentPrintFormat entity = await FindAsync(id);
        entity.Name = string.IsNullOrWhiteSpace(request.Name) ? entity.Name : request.Name;
        entity.Code = string.IsNullOrWhiteSpace(request.Code) ? entity.Code : request.Code;
        entity.Mode = string.IsNullOrWhiteSpace(request.Mode) ? entity.Mode : request.Mode;
        entity.WidthMm = request.WidthMm;
        entity.IsActive = request.IsActive;
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task DeleteAsync(int id)
    {
        DocumentPrintFormat entity = await FindAsync(id);
        _context.DocumentPrintFormats.Remove(entity);
        await _context.SaveChangesAsync();
    }

    private async Task<DocumentPrintFormat> FindAsync(int id)
    {
        DocumentPrintFormat? entity = await _context.DocumentPrintFormats.FirstOrDefaultAsync(item => item.Id == id);
        if (entity is null)
        {
            throw new AppException("Formato de impresión no encontrado.", 404);
        }

        return entity;
    }
}
