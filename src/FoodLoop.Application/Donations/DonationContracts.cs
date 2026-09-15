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

public sealed record DonationCategoryItem(Guid Id, string Name);

public sealed record DonationOperationResult(bool Succeeded, string? Error = null, Guid? DonationId = null)
{
    public static DonationOperationResult Success(Guid? donationId = null) => new(true, null, donationId);
    public static DonationOperationResult Failure(string error) => new(false, error, null);
}
