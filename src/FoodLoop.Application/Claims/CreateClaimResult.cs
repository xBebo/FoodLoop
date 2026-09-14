namespace FoodLoop.Application.Claims;
public enum CreateClaimOutcome
{
    Created,
    Unauthenticated,
    Forbidden,
    OrganizationNotActive,
    DonorOrganizationNotActive,
    DonationNotFound,
    DonationNotAvailable,
    DonationExpired,
    Conflict
}
public sealed record CreateClaimResult(CreateClaimOutcome Outcome, Guid? ClaimId = null);
