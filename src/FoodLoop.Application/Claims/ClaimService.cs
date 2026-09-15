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
    IClaimRepository claims, IAuditService audit, IUnitOfWork unitOfWork, TimeProvider clock)
{
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
            x.FoodDonation.ExpiresAtUtc, x.Status, x.CreatedAtUtc))]);
    }
}
