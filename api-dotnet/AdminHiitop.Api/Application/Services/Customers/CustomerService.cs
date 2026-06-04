using AdminHiitop.Api.Application.DTOs.Customers;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Customers;

public sealed class CustomerService : ICustomerService
{
    private readonly ICustomerQueryService _customerQueryService;
    private readonly AdminHiitopDbContext _context;

    public CustomerService(ICustomerQueryService customerQueryService, AdminHiitopDbContext context)
    {
        _customerQueryService = customerQueryService;
        _context = context;
    }

    public async Task<object> GetAsync(string? search)
        => (object)await _customerQueryService.GetAsync(search);

    public Task<Customer?> GetByIdAsync(int id)
        => _context.Customers.AsNoTracking()
            .Include(item => item.Province)
            .Include(item => item.District)
            .FirstOrDefaultAsync(item => item.Id == id);

    public async Task<Customer> CreateAsync(Customer request)
    {
        _context.Customers.Add(request);
        await _context.SaveChangesAsync();
        return request;
    }

    public async Task<Customer> UpdateAsync(int id, Customer request)
    {
        Customer entity = await FindAsync(id);
        entity.FullName = request.FullName ?? entity.FullName;
        entity.Dni = request.Dni ?? entity.Dni;
        entity.Phone = request.Phone ?? entity.Phone;
        entity.Email = request.Email ?? entity.Email;
        entity.Address = request.Address ?? entity.Address;
        entity.ProvinceId = request.ProvinceId;
        entity.DistrictId = request.DistrictId;
        entity.DocumentType = request.DocumentType ?? entity.DocumentType;
        entity.Ruc = request.Ruc ?? entity.Ruc;
        entity.RazonSocial = request.RazonSocial ?? entity.RazonSocial;
        entity.NombreComercial = request.NombreComercial ?? entity.NombreComercial;
        entity.IsActive = request.IsActive;
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task DeleteAsync(int id)
    {
        Customer entity = await FindAsync(id);
        _context.Customers.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<List<CustomerMetricsResponse>> GetMetricsAsync(int top)
    {
        // Step 1 — top customers by total spent
        var topCustomers = await _context.Orders
            .Where(o => o.CustomerId.HasValue)
            .GroupBy(o => o.CustomerId!.Value)
            .Select(g => new { CustomerId = g.Key, OrderCount = g.Count(), TotalSpent = g.Sum(o => o.Total) })
            .OrderByDescending(x => x.TotalSpent)
            .Take(top)
            .ToListAsync();

        if (topCustomers.Count == 0) return [];

        var customerIds = topCustomers.Select(x => x.CustomerId).ToList();

        // Step 2 — customer names
        var customers = await _context.Customers
            .Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.FullName, c.Phone, c.Email })
            .ToListAsync();

        // Step 3 — size preferences for those customers
        var sizeRows = await _context.Orders
            .Where(o => o.CustomerId.HasValue && customerIds.Contains(o.CustomerId!.Value))
            .SelectMany(o => o.Items.Where(i => i.Size != null), (o, i) => new { o.CustomerId, i.Size, i.Quantity })
            .GroupBy(x => new { x.CustomerId, x.Size })
            .Select(g => new { g.Key.CustomerId, Size = g.Key.Size!, Quantity = g.Sum(x => x.Quantity) })
            .ToListAsync();

        var sizesByCustomer = sizeRows
            .GroupBy(r => r.CustomerId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Quantity)
                                            .Take(5)
                                            .Select(r => new SizeMetric { Size = r.Size, Quantity = r.Quantity })
                                            .ToList());

        return topCustomers.Select(tc =>
        {
            var cust = customers.FirstOrDefault(c => c.Id == tc.CustomerId);
            return new CustomerMetricsResponse
            {
                CustomerId  = tc.CustomerId,
                FullName    = cust?.FullName ?? "—",
                Phone       = cust?.Phone,
                Email       = cust?.Email,
                OrderCount  = tc.OrderCount,
                TotalSpent  = tc.TotalSpent,
                TopSizes    = sizesByCustomer.GetValueOrDefault(tc.CustomerId) ?? [],
            };
        }).ToList();
    }

    private async Task<Customer> FindAsync(int id)
    {
        Customer? entity = await _context.Customers.FirstOrDefaultAsync(item => item.Id == id);
        if (entity is null) throw new AppException("Cliente no encontrado.", 404);
        return entity;
    }
}
