namespace AdminHiitop.Api.Domain.Catalog.Entities;

public sealed class DocumentTypePrintFormat
{
    public int DocumentTypeId { get; set; }
    public int DocumentPrintFormatId { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public DocumentType DocumentType { get; set; } = null!;
    public DocumentPrintFormat DocumentPrintFormat { get; set; } = null!;
}
