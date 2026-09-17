using FoodLoop.Domain.Enums;
namespace FoodLoop.Application.Claims;
public enum CancelClaimOutcome
{
    Cancelled,
    Unauthenticated,
    Forbidden,
    OrganizationNotActive,
    ClaimNotFound,
    NotCancellable,
    Conflict
}
public sealed record CancelClaimResult(CancelClaimOutcome Outcome, DonationStatus? DonationStatus = null);
