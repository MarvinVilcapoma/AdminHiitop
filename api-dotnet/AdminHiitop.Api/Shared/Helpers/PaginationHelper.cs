using AdminHiitop.Api.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Shared.Helpers;

public static class PaginationHelper
{
    public static async Task<PagedResponse<T>> CreateAsync<T>(
        IQueryable<T> query,
        int page,
        int perPage)
    {
        int safePage = page < 1 ? 1 : page;
        int safePerPage = perPage < 1 ? 15 : perPage;
        int total = await query.CountAsync();
        int lastPage = total == 0 ? 1 : (int)Math.Ceiling(total / (double)safePerPage);

        List<T> items = await query
            .Skip((safePage - 1) * safePerPage)
            .Take(safePerPage)
            .ToListAsync();

        return new PagedResponse<T>
        {
            Data = items,
            CurrentPage = safePage,
            LastPage = lastPage,
            PerPage = safePerPage,
            Total = total
        };
    }
}
