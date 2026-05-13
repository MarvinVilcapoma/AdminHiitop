using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IDocumentPrintFormatService
{
    Task<object> GetAsync(int perPage, int page, string? search);
    Task<DocumentPrintFormat?> GetByIdAsync(int id);
    Task<DocumentPrintFormat> CreateAsync(DocumentPrintFormat request);
    Task<DocumentPrintFormat> UpdateAsync(int id, DocumentPrintFormat request);
    Task DeleteAsync(int id);
}
