namespace AdminHiitop.Api.Application.DTOs.Customers;

public sealed class CustomerListItemResponse
{
    public int Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? Dni { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Ruc { get; init; }
    public string? DocumentType { get; init; }
    public bool IsActive { get; init; }
}
