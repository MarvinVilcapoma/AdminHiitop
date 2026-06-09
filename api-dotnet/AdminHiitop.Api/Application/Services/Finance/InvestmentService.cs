using AdminHiitop.Api.Application.DTOs.Finance;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Finance.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Finance;

public sealed class InvestmentService : IInvestmentService
{
    private readonly AdminHiitopDbContext _context;

    public InvestmentService(AdminHiitopDbContext context)
        => _context = context;

    public async Task<List<InvestmentCategoryResponse>> GetCategoriesAsync()
        => await _context.InvestmentCategories
            .Where(c => c.IsActive && c.DeletedAt == null)
            .OrderBy(c => c.Name)
            .Select(c => new InvestmentCategoryResponse
            {
                Id          = c.Id,
                Code        = c.Code,
                Name        = c.Name,
                Description = c.Description,
                IsActive    = c.IsActive,
            })
            .ToListAsync();

    public async Task<List<InvestmentResponse>> GetAllAsync()
        => await _context.Investments
            .Include(i => i.InvestmentCategory)
            .Where(i => i.DeletedAt == null)
            .OrderByDescending(i => i.InvestmentDate)
            .Select(i => new InvestmentResponse
            {
                Id                   = i.Id,
                InvestmentCategoryId = i.InvestmentCategoryId,
                CategoryName         = i.InvestmentCategory!.Name,
                Amount               = i.Amount,
                Description          = i.Description,
                InvestmentDate       = i.InvestmentDate,
                IsActive             = i.IsActive,
                CreatedAt            = i.CreatedAt,
            })
            .ToListAsync();

    public async Task<InvestmentResponse> CreateAsync(CreateInvestmentRequest request, int? userId = null)
    {
        if (request.Amount <= 0)
            throw new AppException("El monto de inversión debe ser mayor a cero.", 400);

        var category = await _context.InvestmentCategories
            .FirstOrDefaultAsync(c => c.Id == request.InvestmentCategoryId && c.IsActive && c.DeletedAt == null)
            ?? throw new AppException("Categoría de inversión no encontrada.", 404);

        var investment = new Investment
        {
            InvestmentCategoryId = request.InvestmentCategoryId,
            Amount               = request.Amount,
            Description          = request.Description,
            InvestmentDate       = request.InvestmentDate,
            IsActive             = true,
            CreatedBy            = userId,
            CreatedAt            = DateTime.UtcNow,
            UpdatedAt            = DateTime.UtcNow,
        };

        _context.Investments.Add(investment);
        await _context.SaveChangesAsync();

        return new InvestmentResponse
        {
            Id                   = investment.Id,
            InvestmentCategoryId = investment.InvestmentCategoryId,
            CategoryName         = category.Name,
            Amount               = investment.Amount,
            Description          = investment.Description,
            InvestmentDate       = investment.InvestmentDate,
            IsActive             = investment.IsActive,
            CreatedAt            = investment.CreatedAt,
        };
    }

    public async Task<InvestmentResponse> UpdateAsync(int id, UpdateInvestmentRequest request, int? userId = null)
    {
        var investment = await _context.Investments
            .Include(i => i.InvestmentCategory)
            .FirstOrDefaultAsync(i => i.Id == id && i.DeletedAt == null)
            ?? throw new AppException("Inversión no encontrada.", 404);

        if (request.Amount <= 0)
            throw new AppException("El monto de inversión debe ser mayor a cero.", 400);

        var category = await _context.InvestmentCategories
            .FirstOrDefaultAsync(c => c.Id == request.InvestmentCategoryId && c.IsActive && c.DeletedAt == null)
            ?? throw new AppException("Categoría de inversión no encontrada.", 404);

        investment.InvestmentCategoryId = request.InvestmentCategoryId;
        investment.Amount               = request.Amount;
        investment.Description          = request.Description;
        investment.InvestmentDate       = request.InvestmentDate;
        investment.IsActive             = request.IsActive;
        investment.UpdatedBy            = userId;
        investment.UpdatedAt            = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new InvestmentResponse
        {
            Id                   = investment.Id,
            InvestmentCategoryId = investment.InvestmentCategoryId,
            CategoryName         = category.Name,
            Amount               = investment.Amount,
            Description          = investment.Description,
            InvestmentDate       = investment.InvestmentDate,
            IsActive             = investment.IsActive,
            CreatedAt            = investment.CreatedAt,
        };
    }

    public async Task DeleteAsync(int id)
    {
        var investment = await _context.Investments
            .FirstOrDefaultAsync(i => i.Id == id && i.DeletedAt == null)
            ?? throw new AppException("Inversión no encontrada.", 404);

        investment.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}
