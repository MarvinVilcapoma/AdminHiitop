using AdminHiitop.Api.Application.DTOs.Finance;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Finance.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Finance;

public sealed class FinancialMovementService : IFinancialMovementService
{
    private readonly AdminHiitopDbContext _context;

    public FinancialMovementService(AdminHiitopDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResponse<FinancialMovementResponse>> GetPagedAsync(
        string? type, int? categoryId, string? paymentMethod,
        DateTime? dateFrom, DateTime? dateTo, int? year, int? month,
        int page, int perPage)
    {
        IQueryable<FinancialMovement> query = _context.FinancialMovements
            .Include(m => m.Category)
            .OrderByDescending(m => m.MovementDate)
            .ThenByDescending(m => m.Id);

        if (!string.IsNullOrEmpty(type))
            query = query.Where(m => m.Type == type.ToUpperInvariant());

        if (categoryId.HasValue)
            query = query.Where(m => m.CategoryId == categoryId.Value);

        if (!string.IsNullOrEmpty(paymentMethod))
            query = query.Where(m => m.PaymentMethod == paymentMethod);

        if (dateFrom.HasValue)
            query = query.Where(m => m.MovementDate >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(m => m.MovementDate <= dateTo.Value.AddDays(1).AddSeconds(-1));

        if (year.HasValue)
            query = query.Where(m => m.MovementDate.Year == year.Value);

        if (month.HasValue)
            query = query.Where(m => m.MovementDate.Month == month.Value);

        int total = await query.CountAsync();

        // Materialize first — EF Core cannot translate C# method calls inside Select() to SQL.
        List<FinancialMovement> raw = await query
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .ToListAsync();

        List<FinancialMovementResponse> items = raw.Select(MapToResponse).ToList();

        return new PagedResponse<FinancialMovementResponse>
        {
            Data        = items,
            Total       = total,
            CurrentPage = page,
            PerPage     = perPage,
            LastPage    = (int)Math.Ceiling((double)total / perPage),
        };
    }

    public async Task<FinancialMovementResponse?> GetByIdAsync(int id)
    {
        FinancialMovement? entity = await _context.FinancialMovements
            .Include(m => m.Category)
            .FirstOrDefaultAsync(m => m.Id == id);

        return entity is null ? null : MapToResponse(entity);
    }

    public async Task<FinancialMovementResponse> CreateAsync(FinancialMovementRequest request, int? userId)
    {
        Validate(request);
        await EnsureCategoryExistsAndMatchesTypeAsync(request.CategoryId, request.Type);

        var entity = new FinancialMovement
        {
            Type          = request.Type.ToUpperInvariant(),
            CategoryId    = request.CategoryId,
            Description   = request.Description.Trim(),
            Amount        = request.Amount,
            MovementDate  = request.MovementDate,
            PaymentMethod = request.PaymentMethod,
            Reference     = request.Reference,
            Notes         = request.Notes,
            SourceType    = "MANUAL",
            CreatedBy     = userId,
            UpdatedBy     = userId,
        };

        _context.FinancialMovements.Add(entity);
        await _context.SaveChangesAsync();

        await _context.Entry(entity).Reference(m => m.Category).LoadAsync();
        return MapToResponse(entity);
    }

    public async Task<FinancialMovementResponse> UpdateAsync(int id, FinancialMovementRequest request, int? userId)
    {
        FinancialMovement entity = await _context.FinancialMovements
            .Include(m => m.Category)
            .FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new AppException("Movimiento no encontrado.");

        Validate(request);
        await EnsureCategoryExistsAndMatchesTypeAsync(request.CategoryId, request.Type);

        entity.Type          = request.Type.ToUpperInvariant();
        entity.CategoryId    = request.CategoryId;
        entity.Description   = request.Description.Trim();
        entity.Amount        = request.Amount;
        entity.MovementDate  = request.MovementDate;
        entity.PaymentMethod = request.PaymentMethod;
        entity.Reference     = request.Reference;
        entity.Notes         = request.Notes;
        entity.UpdatedBy     = userId;

        await _context.SaveChangesAsync();
        await _context.Entry(entity).Reference(m => m.Category).LoadAsync();
        return MapToResponse(entity);
    }

    public async Task DeleteAsync(int id)
    {
        FinancialMovement entity = await _context.FinancialMovements.FindAsync(id)
            ?? throw new AppException("Movimiento no encontrado.");

        _context.FinancialMovements.Remove(entity);
        await _context.SaveChangesAsync();
    }

    private async Task EnsureCategoryExistsAndMatchesTypeAsync(int categoryId, string type)
    {
        var category = await _context.FinancialCategories.FindAsync(categoryId)
            ?? throw new AppException("Categoría no encontrada.");

        if (!category.IsActive)
            throw new AppException("La categoría está inactiva.");

        if (!string.IsNullOrEmpty(type) && category.Type != type.ToUpperInvariant())
            throw new AppException($"La categoría '{category.Name}' es de tipo {category.Type}, no {type.ToUpperInvariant()}.");
    }

    private static void Validate(FinancialMovementRequest r)
    {
        if (r.Amount <= 0)
            throw new AppException("El monto debe ser mayor que 0.");

        if (r.Type?.ToUpperInvariant() is not ("EXPENSE" or "INCOME"))
            throw new AppException("El tipo debe ser EXPENSE o INCOME.");
    }

    private static FinancialMovementResponse MapToResponse(FinancialMovement m) => new()
    {
        Id               = m.Id,
        Type             = m.Type,
        CategoryId       = m.CategoryId,
        CategoryName     = m.Category?.Name,
        CategoryColor    = m.Category?.Color,
        CategoryIcon     = m.Category?.Icon,
        Description      = m.Description,
        Amount           = m.Amount,
        MovementDate     = m.MovementDate,
        PaymentMethod    = m.PaymentMethod,
        Reference        = m.Reference,
        Notes            = m.Notes,
        SourceType       = m.SourceType,
        SourceId         = m.SourceId,
        IsFixedGenerated = m.IsFixedGenerated,
        CreatedAt        = m.CreatedAt,
        UpdatedAt        = m.UpdatedAt,
    };
}
