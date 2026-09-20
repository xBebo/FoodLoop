using FoodLoop.Application.Admin;
using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Domain.Enums;
using Microsoft.EntityFrameworkCore;
namespace FoodLoop.Infrastructure.Persistence.Repositories;
public sealed class AdminReadRepository(ApplicationDbContext db) : IAdminReadRepository
{
    public async Task<DashboardSummary> GetDashboardAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        // Execute sequentially: one scoped DbContext cannot run parallel queries.
        var pending = await db.Organizations.CountAsync(x => x.Status == OrganizationStatus.Pending, ct);
        var available = await db.FoodDonations.CountAsync(x => x.Status == DonationStatus.Available
            && x.ExpiresAtUtc > now && x.DonorOrganization.Status == OrganizationStatus.Active
            && x.DonorOrganization.Type == OrganizationType.Donor, ct);
        // Count completed claims once, not quantities or handover rows. Require delivery evidence.
        var closed = await db.DonationClaims.CountAsync(x => x.Status == ClaimStatus.Closed
            && x.FoodDonation.Status == DonationStatus.Closed
            && db.HandoverRecords.Any(h => h.DonationClaimId == x.Id && h.Type == HandoverType.Delivery), ct);
        var cancelled = await db.DonationClaims.CountAsync(x => x.Status == ClaimStatus.Cancelled, ct);
        var expired = await db.FoodDonations.CountAsync(x => x.Status == DonationStatus.Expired, ct);
        return new DashboardSummary(pending, available, closed, cancelled, expired);
    }
    public async Task<AuditPage> GetAuditPageAsync(int page, int pageSize, AuditFilter? filter = null, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);
        filter ??= new AuditFilter();
        var query = from entry in db.AuditLogs.AsNoTracking()
            join u in db.Users.AsNoTracking() on entry.ActorUserId equals (Guid?)u.Id into actors
            from actor in actors.DefaultIfEmpty()
            let actorName = entry.ActorUserId == null ? "System" : actor == null ? "Unknown user"
                : actor.DisplayName != "" ? actor.DisplayName : actor.UserName ?? "Unnamed user"
            select new { entry, actorName };
        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            var action = filter.Action.Trim();
            query = query.Where(x => x.entry.Action == action);
        }
        if (!string.IsNullOrWhiteSpace(filter.Actor))
        {
            var actor = filter.Actor.Trim();
            query = query.Where(x => x.actorName.Contains(actor));
        }
        if (filter.FromUtc.HasValue) query = query.Where(x => x.entry.CreatedAtUtc >= filter.FromUtc.Value);
        if (filter.ToUtc.HasValue) query = query.Where(x => x.entry.CreatedAtUtc < filter.ToUtc.Value);
        // Filters must apply before Count, Skip and Take so TotalCount reflects the filtered set.
        var total = await query.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        var items = await query.OrderByDescending(x => x.entry.CreatedAtUtc).ThenByDescending(x => x.entry.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AuditEntry(x.entry.Id, x.entry.Action, x.entry.ActorUserId, x.actorName,
                x.entry.EntityType, x.entry.EntityId, x.entry.CreatedAtUtc))
            .ToListAsync(ct);
        return new AuditPage(items, page, pageSize, total) { Filter = filter };
    }
}
