using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.Donations;

public sealed record CreateDonationRequest(
    Guid FoodCategoryId,
    string Title,
    string Description,
    decimal Quantity,
    QuantityUnit Unit,
    DateTimeOffset PreparedAt,
    DateTimeOffset ExpiresAt,
    string StorageInstructions,
    string PickupAddress);

public sealed record UpdateDonationRequest(
    Guid FoodCategoryId,
    string Title,
    string Description,
    decimal Quantity,
    QuantityUnit Unit,
    DateTimeOffset PreparedAt,
    DateTimeOffset ExpiresAt,
    string StorageInstructions,
    string PickupAddress,
    string RowVersion);

public sealed record DonationEditItem(
    Guid Id,
    Guid FoodCategoryId,
    string Title,
    string Description,
    decimal Quantity,
    QuantityUnit Unit,
    DateTimeOffset PreparedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string StorageInstructions,
    string PickupAddress,
    string RowVersion);

public sealed record DonationListItem(
    Guid Id,
    string Title,
    string Category,
    decimal Quantity,
    QuantityUnit Unit,
    DateTimeOffset PreparedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DonationStatus Status,
    string PickupAddress,
    string? DonorName = null);

public sealed record DonationDetailsItem(
    Guid Id,
    string Title,
    string Description,
    string Category,
    decimal Quantity,
    QuantityUnit Unit,
    DateTimeOffset PreparedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string PickupAddress,
    string StorageInstructions,
    DonationStatus Status);

public sealed record DonationExpiryResult(bool Succeeded, int ExpiredCount, string? Error = null)
{
    public static DonationExpiryResult Success(int expiredCount) => new(true, expiredCount);
    public static DonationExpiryResult Conflict() => new(false, 0, "One or more donations changed while expiry was running.");
}

public sealed record DonationCategoryItem(Guid Id, string Name);

public sealed record AvailableDonationsPage(
    IReadOnlyList<DonationListItem> Items,
    int Page,
    bool HasPrevious,
    bool HasNext,
    string Search,
    Guid? CategoryId);

public enum GetDonationForEditOutcome
{
    Success,
    NotFound,
    Forbidden,
    NotDraft
}

public sealed record GetDonationForEditResult(GetDonationForEditOutcome Outcome, DonationEditItem? Donation = null);

// Transport-neutral failure categories; the web layer decides how each one is presented.
public enum DonationFailureKind
{
    Validation,
    Forbidden,
    NotFound,
    InvalidState,
    Conflict
}

public sealed record DonationOperationResult(
    bool Succeeded,
    string? Error = null,
    Guid? DonationId = null,
    DonationFailureKind? Failure = null)
{
    public static DonationOperationResult Success(Guid? donationId = null) => new(true, null, donationId);
    public static DonationOperationResult Fail(DonationFailureKind failure, string error) => new(false, error, null, failure);
}
