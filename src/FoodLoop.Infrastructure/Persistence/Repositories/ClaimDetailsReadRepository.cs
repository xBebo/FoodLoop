using FoodLoop.Application.Claims;
using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace FoodLoop.Infrastructure.Persistence.Repositories;
public sealed class ClaimDetailsReadRepository(ApplicationDbContext db) : IClaimDetailsReadRepository
{
    // Only these audit actions are timeline evidence. QR tokens are never evidence: UsedAtUtc also marks revocation.
    private static readonly string[] TimelineActions = ["CourierAssigned", "ClaimCancelled"];

    public async Task<ClaimDetailsEvidence?> GetForBeneficiaryOrganizationAsync(Guid claimId, Guid organizationId, CancellationToken ct = default)
    {
        var claim = await db.DonationClaims.AsNoTracking()
            .Where(x => x.Id == claimId && x.BeneficiaryOrganizationId == organizationId)
            .Select(x => new
            {
                x.Status, x.CreatedAtUtc, HasAssignedCourier = x.AssignedCourierUserId != null,
                CourierDisplayName = db.Users.Where(u => u.Id == x.AssignedCourierUserId).Select(u => u.DisplayName).FirstOrDefault(),
                Donation = new ClaimDonationSummary(x.FoodDonation.Title, x.FoodDonation.FoodCategory.Name, x.FoodDonation.Quantity,
                    x.FoodDonation.Unit, x.FoodDonation.PreparedAtUtc, x.FoodDonation.ExpiresAtUtc, x.FoodDonation.PickupAddress,
                    x.FoodDonation.StorageInstructions, x.FoodDonation.Description, x.FoodDonation.Status)
            })
            .SingleOrDefaultAsync(ct);
        if (claim is null) return null;

        // Execute sequentially: one scoped DbContext cannot run parallel queries.
        var audits = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == nameof(DonationClaim) && a.EntityId == claimId && TimelineActions.Contains(a.Action))
            .Select(a => new ClaimAuditEvidence(a.Id, a.Action, a.CreatedAtUtc)).ToListAsync(ct);
        var handovers = await db.HandoverRecords.AsNoTracking().Where(h => h.DonationClaimId == claimId)
            .Select(h => new ClaimHandoverEvidence(h.Id, h.Type, h.CompletedAtUtc)).ToListAsync(ct);
        return new(claimId, claim.Status, claim.CreatedAtUtc, claim.HasAssignedCourier, claim.CourierDisplayName, claim.Donation, audits, handovers);
    }
}
