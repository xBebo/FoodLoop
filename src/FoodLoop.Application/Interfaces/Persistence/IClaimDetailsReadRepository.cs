using FoodLoop.Application.Claims;
using FoodLoop.Domain.Enums;
namespace FoodLoop.Application.Interfaces.Persistence;
public interface IClaimDetailsReadRepository
{
    // Untracked. Scoped by claim AND owning organization: another organization's claim is null, exactly like a missing one.
    Task<ClaimDetailsEvidence?> GetForBeneficiaryOrganizationAsync(Guid claimId, Guid organizationId, CancellationToken ct = default);
}
// Raw courier DisplayName (may be empty); ClaimService applies the safe fallback. The courier id itself is never read out.
public sealed record ClaimDetailsEvidence(
    Guid ClaimId, ClaimStatus Status, DateTimeOffset CreatedAtUtc, bool HasAssignedCourier, string? CourierDisplayName,
    ClaimDonationSummary Donation, IReadOnlyList<ClaimAuditEvidence> Audits, IReadOnlyList<ClaimHandoverEvidence> Handovers);
// Allowlisted audit metadata only: no Details, no actor.
public sealed record ClaimAuditEvidence(Guid Id, string Action, DateTimeOffset CreatedAtUtc);
public sealed record ClaimHandoverEvidence(Guid Id, HandoverType Type, DateTimeOffset CompletedAtUtc);
