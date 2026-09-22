using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Application.Organizations;
using FoodLoop.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Infrastructure.Persistence.Repositories;

public sealed class OrganizationAdminReadRepository(ApplicationDbContext db) : IOrganizationAdminReadRepository
{
    public async Task<OrganizationAdminPage> GetPageAsync(
        OrganizationStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);

        var query = db.Organizations.AsNoTracking().AsQueryable();
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);

        var total = await query.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        var items = await query
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new OrganizationAdminItem(x.Id, x.Name, x.Type, x.LicenseNumber, x.Status))
            .ToListAsync(ct);

        return new OrganizationAdminPage(items, page, pageSize, total, status);
    }

    public async Task<IReadOnlyList<PendingOrganizationItem>> GetPendingAsync(CancellationToken ct = default) =>
        await db.Organizations.AsNoTracking()
            .Where(x => x.Status == OrganizationStatus.Pending)
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .Select(x => new PendingOrganizationItem(x.Id, x.Name, x.Type, x.LicenseNumber, x.CreatedAtUtc))
            .ToListAsync(ct);
}
