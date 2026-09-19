using FoodLoop.Application.Exceptions;
using FoodLoop.Application.Identity;
using FoodLoop.Application.Interfaces.Auditing;
using FoodLoop.Application.Interfaces.Identity;
using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
namespace FoodLoop.Application.Claims;
public sealed class ClaimService(
    ICurrentUserService currentUser, IFoodDonationRepository donations, IRepository<Organization> organizations,
    IClaimRepository claims, IClaimDetailsReadRepository claimDetails, IAuditService audit, IUnitOfWork unitOfWork, TimeProvider clock)
{
    private const string AssignedCourierFallback = "Courier assigned";

    public async Task<CreateClaimResult> CreateAsync(Guid donationId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) return new(CreateClaimOutcome.Unauthenticated);
        if (!currentUser.IsInRole(AppRoles.Beneficiary)) return new(CreateClaimOutcome.Forbidden);

        var organizationId = await currentUser.GetOrganizationIdAsync(ct);
        var beneficiary = organizationId is Guid id ? await organizations.GetByIdAsync(id, ct) : null;
        if (beneficiary is not { Type: OrganizationType.Beneficiary, Status: OrganizationStatus.Active })
            return new(CreateClaimOutcome.OrganizationNotActive);

        var donation = await donations.GetByIdAsync(donationId, ct);
        if (donation is null) return new(CreateClaimOutcome.DonationNotFound);
        if (donation.Status != DonationStatus.Available) return new(CreateClaimOutcome.DonationNotAvailable);
        var now = clock.GetUtcNow();
        // Expiry transitions belong to the Donations/Expiry module; only reject here.
        if (donation.ExpiresAtUtc <= now) return new(CreateClaimOutcome.DonationExpired);

        var donor = await organizations.GetByIdAsync(donation.DonorOrganizationId, ct);
        if (donor is not { Type: OrganizationType.Donor, Status: OrganizationStatus.Active })
            return new(CreateClaimOutcome.DonorOrganizationNotActive);

        // Business pre-check only. Double booking is guaranteed by the donation RowVersion plus UX_Claim_ActiveDonation,
        // because another request can still create an active claim after this query.
        if (await claims.GetActiveForDonationAsync(donation.Id, ct) is not null) return new(CreateClaimOutcome.Conflict);

        donation.Status = DonationStatus.Claimed;
        var claim = new DonationClaim
        {
            FoodDonationId = donation.Id, BeneficiaryOrganizationId = beneficiary.Id,
            Status = ClaimStatus.Booked, CreatedAtUtc = now
        };
        claims.Add(claim);
        audit.Record("ClaimCreated", nameof(DonationClaim), claim.Id,
            $"DonationId={donation.Id}; DonationStatus={DonationStatus.Available}->{DonationStatus.Claimed}");
        try { await unitOfWork.SaveChangesAsync(ct); }
        catch (PersistenceConflictException) { return new(CreateClaimOutcome.Conflict); }
        return new(CreateClaimOutcome.Created, claim.Id);
    }

    public async Task<CancelClaimResult> CancelAsync(Guid claimId, CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated) return new(CancelClaimOutcome.Unauthenticated);
        if (!currentUser.IsInRole(AppRoles.Beneficiary)) return new(CancelClaimOutcome.Forbidden);

        var organizationId = await currentUser.GetOrganizationIdAsync(ct);
        var beneficiary = organizationId is Guid id ? await organizations.GetByIdAsync(id, ct) : null;
        if (beneficiary is not { Type: OrganizationType.Beneficiary }) return new(CancelClaimOutcome.OrganizationNotActive);

        // Ownership is filtered in SQL before anything else is revealed: another organization's claim is indistinguishable from a missing one.
        var claim = await claims.GetByIdForBeneficiaryOrganizationAsync(claimId, beneficiary.Id, ct);
        if (claim is null) return new(CancelClaimOutcome.ClaimNotFound);
        // Suspended keeps read-only history (GetMyClaimsAsync) but may not cancel.
        if (beneficiary.Status != OrganizationStatus.Active) return new(CancelClaimOutcome.OrganizationNotActive);
        if (claim is not { Status: ClaimStatus.Booked, AssignedCourierUserId: null }) return new(CancelClaimOutcome.NotCancellable);

        var donation = await donations.GetByIdAsync(claim.FoodDonationId, ct);
        if (donation is not { Status: DonationStatus.Claimed }) return new(CancelClaimOutcome.NotCancellable);

        var donor = await organizations.GetByIdAsync(donation.DonorOrganizationId, ct);
        var now = clock.GetUtcNow();
        // Only a donation that could be claimed again returns to the marketplace; anything else goes back to its donor as Draft.
        // Expiry transitions belong to the Donations/Expiry module, so Expired is never written here.
        var donationStatus = donor is { Type: OrganizationType.Donor, Status: OrganizationStatus.Active } && donation.ExpiresAtUtc > now
            ? DonationStatus.Available : DonationStatus.Draft;

        // Claim and donation RowVersions make this lose cleanly against a concurrent courier assignment or duplicate cancel.
        claim.Status = ClaimStatus.Cancelled;
        donation.Status = donationStatus;
        audit.Record("ClaimCancelled", nameof(DonationClaim), claim.Id,
            $"DonationId={donation.Id}; DonationStatus={DonationStatus.Claimed}->{donationStatus}");
        try { await unitOfWork.SaveChangesAsync(ct); }
        catch (PersistenceConflictException) { return new(CancelClaimOutcome.Conflict); }
        return new(CancelClaimOutcome.Cancelled, donationStatus);
    }

    public async Task<GetMyClaimsResult> GetMyClaimsAsync(int page, int pageSize, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) return new(GetMyClaimsOutcome.Unauthenticated, []);
        if (!currentUser.IsInRole(AppRoles.Beneficiary)) return new(GetMyClaimsOutcome.Forbidden, []);

        var organizationId = await currentUser.GetOrganizationIdAsync(ct);
        var beneficiary = organizationId is Guid id ? await organizations.GetByIdAsync(id, ct) : null;
        if (beneficiary is not { Type: OrganizationType.Beneficiary }) return new(GetMyClaimsOutcome.OrganizationNotBeneficiary, []);
        // Allowlist: Active, plus Suspended for read-only access to its own history. Pending, Rejected and any future status are denied.
        if (beneficiary.Status is not (OrganizationStatus.Active or OrganizationStatus.Suspended)) return new(GetMyClaimsOutcome.Forbidden, []);

        var items = await claims.GetForBeneficiaryOrganizationAsync(beneficiary.Id, page, pageSize, ct);
        return new(GetMyClaimsOutcome.Success, [.. items.Select(x => new ClaimSummary(
            x.Id, x.FoodDonationId, x.FoodDonation.Title, x.FoodDonation.Quantity, x.FoodDonation.Unit, x.FoodDonation.PickupAddress,
            x.FoodDonation.ExpiresAtUtc, x.Status, x.CreatedAtUtc,
            CanCancel(beneficiary.Status, x.Status, x.AssignedCourierUserId is not null, x.FoodDonation.Status)))]);
    }

    public async Task<GetClaimDetailsResult> GetDetailsAsync(Guid claimId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) return new(GetClaimDetailsOutcome.Unauthenticated);
        if (!currentUser.IsInRole(AppRoles.Beneficiary)) return new(GetClaimDetailsOutcome.Forbidden);

        var organizationId = await currentUser.GetOrganizationIdAsync(ct);
        var beneficiary = organizationId is Guid id ? await organizations.GetByIdAsync(id, ct) : null;
        if (beneficiary is not { Type: OrganizationType.Beneficiary }) return new(GetClaimDetailsOutcome.OrganizationNotBeneficiary);
        // Same allowlist as GetMyClaimsAsync, resolved before any claim lookup: Active, plus Suspended read-only.
        if (beneficiary.Status is not (OrganizationStatus.Active or OrganizationStatus.Suspended)) return new(GetClaimDetailsOutcome.OrganizationNotActive);

        // Ownership is filtered in SQL: another organization's claim is indistinguishable from a missing one.
        var claim = await claimDetails.GetForBeneficiaryOrganizationAsync(claimId, beneficiary.Id, ct);
        if (claim is null) return new(GetClaimDetailsOutcome.NotFound);

        var courier = !claim.HasAssignedCourier ? null
            : string.IsNullOrWhiteSpace(claim.CourierDisplayName) ? AssignedCourierFallback : claim.CourierDisplayName.Trim();
        return new(GetClaimDetailsOutcome.Success, new ClaimDetails(claim.ClaimId, claim.Status, claim.CreatedAtUtc,
            CanCancel(beneficiary.Status, claim.Status, claim.HasAssignedCourier, claim.Donation.Status),
            courier, claim.Donation, BuildTimeline(claim)));
    }

    // Mirrors CancelAsync's eligibility so Suspended history stays read-only; it never authorizes anything.
    private static bool CanCancel(OrganizationStatus organizationStatus, ClaimStatus claimStatus, bool hasAssignedCourier, DonationStatus donationStatus)
        => organizationStatus == OrganizationStatus.Active && claimStatus == ClaimStatus.Booked && !hasAssignedCourier
            && donationStatus == DonationStatus.Claimed;

    // Only persisted evidence becomes an event; current status alone never fabricates one.
    // Order: time, then fixed rank (Delivery and Closed share a timestamp), then evidence id so equal timestamps stay stable.
    private static IReadOnlyList<ClaimTimelineEvent> BuildTimeline(ClaimDetailsEvidence claim)
    {
        var events = new List<(ClaimTimelineEvent Event, int Rank, Guid EvidenceId)>
        {
            (new("Claimed", "Claimed", claim.CreatedAtUtc), 0, claim.ClaimId)
        };
        var assignments = claim.Audits.Where(x => x.Action == "CourierAssigned").OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).ToList();
        for (var i = 0; i < assignments.Count; i++)
            events.Add((i == 0 ? new("CourierAssigned", "Courier assigned", assignments[i].CreatedAtUtc)
                : new("CourierReassigned", "Courier reassigned", assignments[i].CreatedAtUtc), 1, assignments[i].Id));
        foreach (var cancelled in claim.Audits.Where(x => x.Action == "ClaimCancelled"))
            events.Add((new("Cancelled", "Cancelled", cancelled.CreatedAtUtc), 2, cancelled.Id));
        foreach (var handover in claim.Handovers)
        {
            if (handover.Type == HandoverType.Pickup) events.Add((new("PickupVerified", "Pickup verified", handover.CompletedAtUtc), 3, handover.Id));
            if (handover.Type != HandoverType.Delivery) continue;
            events.Add((new("DeliveryVerified", "Delivery verified", handover.CompletedAtUtc), 4, handover.Id));
            if (claim.Status == ClaimStatus.Closed) events.Add((new("Closed", "Closed", handover.CompletedAtUtc), 5, handover.Id));
        }
        return [.. events.OrderBy(x => x.Event.AtUtc).ThenBy(x => x.Rank).ThenBy(x => x.EvidenceId).Select(x => x.Event)];
    }
}
