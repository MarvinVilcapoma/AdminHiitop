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

    private async Task<Customer> FindAsync(int id)
    {
        Customer? entity = await _context.Customers.FirstOrDefaultAsync(item => item.Id == id);
        if (entity is null) throw new AppException("Cliente no encontrado.", 404);
        return entity;
    }
}
