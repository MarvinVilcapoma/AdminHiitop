using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IDocumentTypeService
{
    Task<object> GetAsync(int? perPage, int page, string? search, bool activeOnly = false);
    Task<object> GetByIdAsync(int id);
    Task<DocumentType> CreateAsync(DocumentType request);
    Task<DocumentType> UpdateAsync(int id, DocumentType request);
    Task DeleteAsync(int id);
}
