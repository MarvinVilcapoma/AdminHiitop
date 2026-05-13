using AdminHiitop.Api.Application.DTOs.Customers;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Customers;

public sealed class CustomerQueryService : ICustomerQueryService
{
    private readonly AdminHiitopDbContext _context;

    public CustomerQueryService(AdminHiitopDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CustomerListItemResponse>> GetAsync(string? search)
    {
        string normalizedSearch = NormalizeSearch(search);
        IQueryable<Domain.Catalog.Entities.Customer> query = _context.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(item =>
                item.FullName.Contains(normalizedSearch) ||
                (item.Dni != null && item.Dni.Contains(normalizedSearch)) ||
                (item.Ruc != null && item.Ruc.Contains(normalizedSearch)) ||
                (item.Email != null && item.Email.Contains(normalizedSearch)));
        }

        return await query
            .OrderBy(item => item.FullName)
            .Select(item => new CustomerListItemResponse
            {
                Id = item.Id,
                FullName = item.FullName,
                Dni = item.Dni,
                Phone = item.Phone,
                Email = item.Email,
                Ruc = item.Ruc,
                DocumentType = item.DocumentType,
                IsActive = item.IsActive
            })
            .ToListAsync();
    }

    private static string NormalizeSearch(string? search) =>
        string.IsNullOrWhiteSpace(search) ? string.Empty : search.Trim();
}
