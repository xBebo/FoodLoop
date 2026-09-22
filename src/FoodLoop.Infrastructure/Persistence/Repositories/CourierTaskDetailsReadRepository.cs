using FoodLoop.Application.Courier;
using FoodLoop.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Infrastructure.Persistence.Repositories;

public sealed class CourierTaskDetailsReadRepository(ApplicationDbContext db) : ICourierTaskDetailsReadRepository
{
    public async Task<CourierTaskDetailsDto?> GetAssignedAsync(
        Guid claimId,
        Guid courierUserId,
        CancellationToken ct = default)
    {
        var claim = await db.DonationClaims
            .AsNoTracking()
            .Include(x => x.FoodDonation)
                .ThenInclude(x => x.DonorOrganization)
            .Include(x => x.BeneficiaryOrganization)
            .FirstOrDefaultAsync(
                x => x.Id == claimId && x.AssignedCourierUserId == courierUserId,
                ct);

        if (claim is null)
        {
            return null;
        }

        var evidence = await db.HandoverRecords
            .AsNoTracking()
            .Where(x => x.DonationClaimId == claimId && x.CourierUserId == courierUserId)
            .OrderBy(x => x.CompletedAtUtc)
            .ToListAsync(ct);

        HandoverEvidenceItem? Evidence(HandoverType type) =>
            evidence.FirstOrDefault(x => x.Type == type) is { } record ? new(record.CompletedAtUtc) : null;

        return new CourierTaskDetailsDto
        {
            ClaimId = claim.Id,
            DonationTitle = claim.FoodDonation.Title,
            DonorOrganizationName = claim.FoodDonation.DonorOrganization.Name,
            BeneficiaryOrganizationName = claim.BeneficiaryOrganization.Name,
            PickupAddress = claim.FoodDonation.PickupAddress,
            ExpiryDate = claim.FoodDonation.ExpiresAtUtc,
            Status = claim.Status,
            NextStepKind = CourierService.NextStep(claim.Status),
            PickupHandoverEvidence = Evidence(HandoverType.Pickup),
            DeliveryHandoverEvidence = Evidence(HandoverType.Delivery)
        };
    }
}
