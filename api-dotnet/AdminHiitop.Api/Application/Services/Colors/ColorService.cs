using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Shared.Exceptions;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Application.Services.Colors;

public sealed class ColorService : IColorService
{
    private readonly AdminHiitopDbContext _context;

    public ColorService(AdminHiitopDbContext context) => _context = context;

    public async Task<object> GetAsync(int perPage, int page, string? search, CancellationToken cancellationToken)
    {
        IQueryable<Color> query = _context.Colors.AsNoTracking().OrderBy(item => item.Name);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(item => item.Name.Contains(search) || item.Slug.Contains(search));
        return await PaginationHelper.CreateAsync(query, page, perPage, cancellationToken);
    }

    public Task<Color?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => _context.Colors.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<Color> CreateAsync(Color request, CancellationToken cancellationToken)
    {
        var entity = new Color
        {
            Name = request.Name,
            HexCode = request.HexCode,
            Slug = string.IsNullOrWhiteSpace(request.Slug) ? request.Name.ToLower().Replace(' ', '-') : request.Slug
        };
        _context.Colors.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<Color> UpdateAsync(int id, Color request, CancellationToken cancellationToken)
    {
        Color entity = await FindAsync(id, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.Name)) entity.Name = request.Name;
        entity.HexCode = request.HexCode ?? entity.HexCode;
        if (!string.IsNullOrWhiteSpace(request.Slug)) entity.Slug = request.Slug;
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        Color entity = await FindAsync(id, cancellationToken);
        _context.Colors.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Color> FindAsync(int id, CancellationToken cancellationToken)
    {
        Color? entity = await _context.Colors.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) throw new AppException("Color no encontrado.", 404);
        return entity;
    }
}
