using AdminHiitop.Api.Application.DTOs.Finance;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Finance.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Finance;

public sealed class FinancialCategoryService : IFinancialCategoryService
{
    private readonly AdminHiitopDbContext _context;

    public FinancialCategoryService(AdminHiitopDbContext context)
    {
        _context = context;
    }

    public async Task<List<FinancialCategoryResponse>> GetAllAsync(string? type = null)
    {
        IQueryable<FinancialCategory> query = _context.FinancialCategories.OrderBy(c => c.Type).ThenBy(c => c.Name);

        if (!string.IsNullOrEmpty(type))
            query = query.Where(c => c.Type == type.ToUpperInvariant());

        return await query.Select(c => MapToResponse(c)).ToListAsync();
    }

    public async Task<FinancialCategoryResponse?> GetByIdAsync(int id)
    {
        FinancialCategory? entity = await _context.FinancialCategories.FindAsync(id);
        return entity is null ? null : MapToResponse(entity);
    }

    public async Task<FinancialCategoryResponse> CreateAsync(FinancialCategoryRequest request)
    {
        if (await _context.FinancialCategories.AnyAsync(c => c.Code == request.Code))
            throw new AppException($"Ya existe una categoría con el código '{request.Code}'.");

        ValidateType(request.Type);

        var entity = new FinancialCategory
        {
            Name        = request.Name.Trim(),
            Code        = request.Code.Trim().ToUpperInvariant(),
            Type        = request.Type.ToUpperInvariant(),
            Description = request.Description,
            Color       = request.Color,
            Icon        = request.Icon,
            IsActive    = request.IsActive,
        };

        _context.FinancialCategories.Add(entity);
        await _context.SaveChangesAsync();
        return MapToResponse(entity);
    }

    public async Task<FinancialCategoryResponse> UpdateAsync(int id, FinancialCategoryRequest request)
    {
        FinancialCategory entity = await _context.FinancialCategories.FindAsync(id)
            ?? throw new AppException("Categoría no encontrada.");

        if (await _context.FinancialCategories.AnyAsync(c => c.Code == request.Code && c.Id != id))
            throw new AppException($"Ya existe otra categoría con el código '{request.Code}'.");

        ValidateType(request.Type);

        entity.Name        = request.Name.Trim();
        entity.Code        = request.Code.Trim().ToUpperInvariant();
        entity.Type        = request.Type.ToUpperInvariant();
        entity.Description = request.Description;
        entity.Color       = request.Color;
        entity.Icon        = request.Icon;
        entity.IsActive    = request.IsActive;

        await _context.SaveChangesAsync();
        return MapToResponse(entity);
    }

    public async Task DeleteAsync(int id)
    {
        FinancialCategory entity = await _context.FinancialCategories.FindAsync(id)
            ?? throw new AppException("Categoría no encontrada.");

        bool inUse = await _context.FinancialMovements.AnyAsync(m => m.CategoryId == id)
                  || await _context.FixedFinancialMovements.AnyAsync(m => m.CategoryId == id);

        if (inUse)
            throw new AppException("No se puede eliminar la categoría porque tiene movimientos asociados.");

        _context.FinancialCategories.Remove(entity);
        await _context.SaveChangesAsync();
    }

    private static void ValidateType(string type)
    {
        if (type?.ToUpperInvariant() is not ("EXPENSE" or "INCOME"))
            throw new AppException("El tipo debe ser EXPENSE o INCOME.");
    }

    private static FinancialCategoryResponse MapToResponse(FinancialCategory c) => new()
    {
        Id          = c.Id,
        Name        = c.Name,
        Code        = c.Code,
        Type        = c.Type,
        Description = c.Description,
        Color       = c.Color,
        Icon        = c.Icon,
        IsActive    = c.IsActive,
        CreatedAt   = c.CreatedAt,
        UpdatedAt   = c.UpdatedAt,
    };
}
