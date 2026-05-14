using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.UnitMeasures;

public sealed class UnitMeasureService : IUnitMeasureService
{
    private readonly AdminHiitopDbContext _context;

    public UnitMeasureService(AdminHiitopDbContext context) => _context = context;

    public async Task<object> GetAsync(int perPage, int page, string? search, CancellationToken cancellationToken)
    {
        var query = _context.UnitMeasures.AsNoTracking().OrderBy(item => item.Name).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(item => item.Name.Contains(search) || item.Code.Contains(search) || (item.SunatCode != null && item.SunatCode.Contains(search)));
        return await PaginationHelper.CreateAsync(query, page, perPage, cancellationToken);
    }

    public Task<UnitMeasure?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => _context.UnitMeasures.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<UnitMeasure> CreateAsync(UnitMeasure request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            request.Code = request.Name.Trim().ToUpper().Replace(" ", "");
        _context.UnitMeasures.Add(request);
        await _context.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<UnitMeasure> UpdateAsync(int id, UnitMeasure request, CancellationToken cancellationToken)
    {
        UnitMeasure entity = await FindAsync(id, cancellationToken);
        entity.Name = string.IsNullOrWhiteSpace(request.Name) ? entity.Name : request.Name;
        entity.Code = string.IsNullOrWhiteSpace(request.Code) ? entity.Code : request.Code;
        entity.SunatCode = request.SunatCode ?? entity.SunatCode;
        entity.IsActive = request.IsActive;
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        UnitMeasure entity = await FindAsync(id, cancellationToken);
        _context.UnitMeasures.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<UnitMeasure> FindAsync(int id, CancellationToken cancellationToken)
    {
        UnitMeasure? entity = await _context.UnitMeasures.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) throw new AppException("Unidad de medida no encontrada.", 404);
        return entity;
    }
}
