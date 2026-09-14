using FoodLoop.Domain.Enums;
namespace FoodLoop.Application.Claims;
public enum GetMyClaimsOutcome
{
    Success,
    Unauthenticated,
    Forbidden,
    OrganizationNotBeneficiary
}
public sealed record ClaimSummary(
    Guid ClaimId, Guid DonationId, string DonationTitle, decimal Quantity, QuantityUnit Unit, string PickupAddress,
    DateTimeOffset ExpiresAtUtc, ClaimStatus ClaimStatus, DateTimeOffset ClaimedAtUtc);
public sealed record GetMyClaimsResult(GetMyClaimsOutcome Outcome, IReadOnlyList<ClaimSummary> Claims);
