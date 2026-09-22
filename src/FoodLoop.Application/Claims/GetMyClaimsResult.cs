using FoodLoop.Domain.Enums;
namespace FoodLoop.Application.Claims;
public enum GetMyClaimsOutcome
{
    Success,
    Unauthenticated,
    Forbidden,
    OrganizationNotBeneficiary
}
// CanCancel is display eligibility only; ClaimService.CancelAsync re-checks every rule on POST.
public sealed record ClaimSummary(
    Guid ClaimId, Guid DonationId, string DonationTitle, decimal Quantity, QuantityUnit Unit, string PickupAddress,
    DateTimeOffset ExpiresAtUtc, ClaimStatus ClaimStatus, DateTimeOffset ClaimedAtUtc, bool CanCancel);
public sealed record GetMyClaimsResult(GetMyClaimsOutcome Outcome, IReadOnlyList<ClaimSummary> Claims, bool HasNext = false);
