using AdminHiitop.Api.Application.DTOs.Finance;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Finance.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Finance;

public sealed class FixedFinancialMovementService : IFixedFinancialMovementService
{
    private readonly AdminHiitopDbContext _context;

    public FixedFinancialMovementService(AdminHiitopDbContext context)
    {
        _context = context;
    }

    public async Task<List<FixedFinancialMovementResponse>> GetAllAsync(string? type = null, bool? isActive = null)
    {
        IQueryable<FixedFinancialMovement> query = _context.FixedFinancialMovements
            .Include(m => m.Category)
            .OrderBy(m => m.Type)
            .ThenBy(m => m.Description);

        if (!string.IsNullOrEmpty(type))
            query = query.Where(m => m.Type == type.ToUpperInvariant());

        if (isActive.HasValue)
            query = query.Where(m => m.IsActive == isActive.Value);

        // Materialize first — EF Core cannot translate C# method calls inside Select() to SQL.
        List<FixedFinancialMovement> raw = await query.ToListAsync();
        return raw.Select(MapToResponse).ToList();
    }

    public async Task<FixedFinancialMovementResponse?> GetByIdAsync(int id)
    {
        FixedFinancialMovement? entity = await _context.FixedFinancialMovements
            .Include(m => m.Category)
            .FirstOrDefaultAsync(m => m.Id == id);

        return entity is null ? null : MapToResponse(entity);
    }

    public async Task<FixedFinancialMovementResponse> CreateAsync(FixedFinancialMovementRequest request, int? userId)
    {
        Validate(request);
        await EnsureCategoryExistsAsync(request.CategoryId, request.Type);

        var entity = new FixedFinancialMovement
        {
            Type          = request.Type.ToUpperInvariant(),
            CategoryId    = request.CategoryId,
            Description   = request.Description.Trim(),
            Amount        = request.Amount,
            Frequency     = request.Frequency.ToUpperInvariant(),
            DayOfMonth    = request.DayOfMonth,
            StartDate     = request.StartDate,
            EndDate       = request.EndDate,
            PaymentMethod = request.PaymentMethod,
            AutoGenerate  = request.AutoGenerate,
            IsActive      = request.IsActive,
            Notes         = request.Notes,
            CreatedBy     = userId,
            UpdatedBy     = userId,
        };

        _context.FixedFinancialMovements.Add(entity);
        await _context.SaveChangesAsync();
        await _context.Entry(entity).Reference(m => m.Category).LoadAsync();
        return MapToResponse(entity);
    }

    public async Task<FixedFinancialMovementResponse> UpdateAsync(int id, FixedFinancialMovementRequest request, int? userId)
    {
        FixedFinancialMovement entity = await _context.FixedFinancialMovements
            .Include(m => m.Category)
            .FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new AppException("Movimiento fijo no encontrado.");

        Validate(request);
        await EnsureCategoryExistsAsync(request.CategoryId, request.Type);

        entity.Type          = request.Type.ToUpperInvariant();
        entity.CategoryId    = request.CategoryId;
        entity.Description   = request.Description.Trim();
        entity.Amount        = request.Amount;
        entity.Frequency     = request.Frequency.ToUpperInvariant();
        entity.DayOfMonth    = request.DayOfMonth;
        entity.StartDate     = request.StartDate;
        entity.EndDate       = request.EndDate;
        entity.PaymentMethod = request.PaymentMethod;
        entity.AutoGenerate  = request.AutoGenerate;
        entity.IsActive      = request.IsActive;
        entity.Notes         = request.Notes;
        entity.UpdatedBy     = userId;

        await _context.SaveChangesAsync();
        await _context.Entry(entity).Reference(m => m.Category).LoadAsync();
        return MapToResponse(entity);
    }

    public async Task DeleteAsync(int id)
    {
        FixedFinancialMovement entity = await _context.FixedFinancialMovements.FindAsync(id)
            ?? throw new AppException("Movimiento fijo no encontrado.");

        _context.FixedFinancialMovements.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<int> GenerateMonthAsync(int year, int month, int? userId)
    {
        DateTime periodStart = new(year, month, 1);
        DateTime periodEnd   = periodStart.AddMonths(1).AddDays(-1);

        List<FixedFinancialMovement> actives = await _context.FixedFinancialMovements
            .Include(m => m.Category)
            .Where(m => m.IsActive && m.AutoGenerate
                && m.StartDate <= periodEnd
                && (m.EndDate == null || m.EndDate >= periodStart))
            .ToListAsync();

        int generated = 0;

        foreach (FixedFinancialMovement fixed_ in actives)
        {
            var dates = GetOccurrenceDates(fixed_, year, month, periodStart, periodEnd);

            foreach (DateTime movDate in dates)
            {
                // Skip if already generated for this exact date
                bool alreadyExists = await _context.FinancialMovements.AnyAsync(m =>
                    m.SourceType == "FIXED"
                    && m.SourceId == fixed_.Id
                    && m.MovementDate.Year  == movDate.Year
                    && m.MovementDate.Month == movDate.Month
                    && m.MovementDate.Day   == movDate.Day);

                if (alreadyExists) continue;

                _context.FinancialMovements.Add(new FinancialMovement
                {
                    Type             = fixed_.Type,
                    CategoryId       = fixed_.CategoryId,
                    Description      = fixed_.Description,
                    Amount           = fixed_.Amount,
                    MovementDate     = movDate,
                    PaymentMethod    = fixed_.PaymentMethod,
                    Notes            = fixed_.Notes,
                    SourceType       = "FIXED",
                    SourceId         = fixed_.Id,
                    IsFixedGenerated = true,
                    CreatedBy        = userId,
                    UpdatedBy        = userId,
                });
                generated++;
            }
        }

        if (generated > 0)
            await _context.SaveChangesAsync();

        return generated;
    }

    /// <summary>
    /// Returns all dates within the given month that match the recurrence rule.
    /// MONTHLY → one date: the specified day of month.
    /// WEEKLY  → all dates in the month whose weekday matches DayOfMonth
    ///           (1=Monday … 7=Sunday, ISO 8601).
    /// YEARLY  → one date per year: StartDate's month + DayOfMonth. Only fires
    ///           if the requested month matches.
    /// </summary>
    private static List<DateTime> GetOccurrenceDates(
        FixedFinancialMovement m, int year, int month,
        DateTime periodStart, DateTime periodEnd)
    {
        string freq = m.Frequency?.ToUpperInvariant() ?? "MONTHLY";

        if (freq == "MONTHLY")
        {
            int day = Math.Min(m.DayOfMonth ?? 1, DateTime.DaysInMonth(year, month));
            return [new DateTime(year, month, day)];
        }

        if (freq == "WEEKLY")
        {
            // DayOfMonth stores ISO day-of-week: 1=Monday, 7=Sunday
            int isoDow = m.DayOfMonth ?? 1;
            isoDow = Math.Clamp(isoDow, 1, 7);

            // Convert ISO to .NET DayOfWeek (0=Sunday, 1=Monday … 6=Saturday)
            DayOfWeek target = isoDow == 7
                ? DayOfWeek.Sunday
                : (DayOfWeek)isoDow;

            var dates = new List<DateTime>();
            for (var d = periodStart; d <= periodEnd; d = d.AddDays(1))
                if (d.DayOfWeek == target) dates.Add(d);
            return dates;
        }

        if (freq == "YEARLY")
        {
            // Only fires in the month that matches StartDate's month
            if (m.StartDate.Month != month) return [];
            int day = Math.Min(m.DayOfMonth ?? m.StartDate.Day, DateTime.DaysInMonth(year, month));
            return [new DateTime(year, month, day)];
        }

        return [];
    }

    private async Task EnsureCategoryExistsAsync(int categoryId, string type)
    {
        var cat = await _context.FinancialCategories.FindAsync(categoryId)
            ?? throw new AppException("Categoría no encontrada.");

        if (!string.IsNullOrEmpty(type) && cat.Type != type.ToUpperInvariant())
            throw new AppException($"La categoría '{cat.Name}' es de tipo {cat.Type}, no {type.ToUpperInvariant()}.");
    }

    private static void Validate(FixedFinancialMovementRequest r)
    {
        if (r.Amount <= 0)
            throw new AppException("El monto debe ser mayor que 0.");

        if (r.Type?.ToUpperInvariant() is not ("EXPENSE" or "INCOME"))
            throw new AppException("El tipo debe ser EXPENSE o INCOME.");

        if (r.Frequency?.ToUpperInvariant() is not ("MONTHLY" or "WEEKLY" or "YEARLY"))
            throw new AppException("La frecuencia debe ser MONTHLY, WEEKLY o YEARLY.");
    }

    private static FixedFinancialMovementResponse MapToResponse(FixedFinancialMovement m) => new()
    {
        Id            = m.Id,
        Type          = m.Type,
        CategoryId    = m.CategoryId,
        CategoryName  = m.Category?.Name,
        CategoryColor = m.Category?.Color,
        CategoryIcon  = m.Category?.Icon,
        Description   = m.Description,
        Amount        = m.Amount,
        Frequency     = m.Frequency,
        DayOfMonth    = m.DayOfMonth,
        StartDate     = m.StartDate,
        EndDate       = m.EndDate,
        PaymentMethod = m.PaymentMethod,
        AutoGenerate  = m.AutoGenerate,
        IsActive      = m.IsActive,
        Notes         = m.Notes,
        CreatedAt     = m.CreatedAt,
        UpdatedAt     = m.UpdatedAt,
    };
}
