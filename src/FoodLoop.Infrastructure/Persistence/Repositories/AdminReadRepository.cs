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
        return new DashboardSummary(pending, available, closed);
    }
    public async Task<AuditPage> GetAuditPageAsync(int page, int pageSize, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);
        var total = await db.AuditLogs.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        var items = await (from entry in db.AuditLogs.AsNoTracking()
            join actor in db.Users.AsNoTracking() on entry.ActorUserId equals (Guid?)actor.Id into actors
            from actor in actors.DefaultIfEmpty()
            orderby entry.CreatedAtUtc descending, entry.Id descending
            select new AuditEntry(entry.Id, entry.Action, entry.ActorUserId,
                entry.ActorUserId == null ? "System" : actor == null ? "Unknown user"
                    : actor.DisplayName != "" ? actor.DisplayName : actor.UserName ?? "Unnamed user",
                entry.EntityType, entry.EntityId, entry.CreatedAtUtc))
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new AuditPage(items, page, pageSize, total);
    }
}
