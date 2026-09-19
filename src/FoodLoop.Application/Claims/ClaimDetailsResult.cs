using FoodLoop.Domain.Enums;
namespace FoodLoop.Application.Claims;
public enum GetClaimDetailsOutcome
{
    Success,
    Unauthenticated,
    Forbidden,
    OrganizationNotBeneficiary,
    OrganizationNotActive,
    NotFound
}
// Donation fields only: no donor organization data.
public sealed record ClaimDonationSummary(
    string Title, string CategoryName, decimal Quantity, QuantityUnit Unit, DateTimeOffset PreparedAtUtc, DateTimeOffset ExpiresAtUtc,
    string PickupAddress, string StorageInstructions, string Description, DonationStatus Status);
// Curated from persisted evidence only; never carries raw audit Details, actors or QR data.
public sealed record ClaimTimelineEvent(string Kind, string Label, DateTimeOffset AtUtc);
// CanCancel is display eligibility only; ClaimService.CancelAsync re-checks every rule on POST.
// CourierDisplayName is null when unassigned and never an email, user name or id.
public sealed record ClaimDetails(
    Guid ClaimId, ClaimStatus Status, DateTimeOffset ClaimedAtUtc, bool CanCancel, string? CourierDisplayName,
    ClaimDonationSummary Donation, IReadOnlyList<ClaimTimelineEvent> Timeline);
public sealed record GetClaimDetailsResult(GetClaimDetailsOutcome Outcome, ClaimDetails? Details = null);
