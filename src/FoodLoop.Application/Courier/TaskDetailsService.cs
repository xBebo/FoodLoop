using FoodLoop.Application.Identity;
using FoodLoop.Application.Interfaces.Identity;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.Courier;

public interface ICourierTaskDetailsReadRepository
{
    Task<CourierTaskDetailsDto?> GetAssignedAsync(Guid claimId, Guid courierUserId, CancellationToken ct = default);
}

public sealed class TaskDetailsService(
    ICourierTaskDetailsReadRepository repository,
    ICurrentUserService currentUser)
{
    public Task<CourierTaskDetailsDto?> GetTaskDetailsAsync(Guid claimId, CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated ||
            !currentUser.IsInRole(AppRoles.Courier) ||
            currentUser.UserId is not Guid courierUserId)
        {
            return Task.FromResult<CourierTaskDetailsDto?>(null);
        }

        return repository.GetAssignedAsync(claimId, courierUserId, ct);
    }
}

public sealed class CourierTaskDetailsDto
{
    public Guid ClaimId { get; init; }
    public string DonationTitle { get; init; } = string.Empty;
    public string DonorOrganizationName { get; init; } = string.Empty;
    public string BeneficiaryOrganizationName { get; init; } = string.Empty;
    public string PickupAddress { get; init; } = string.Empty;
    public DateTimeOffset ExpiryDate { get; init; }
    public ClaimStatus Status { get; init; }
    public string NextStep { get; init; } = string.Empty;
    public HandoverRecord? PickupHandoverEvidence { get; init; }
    public HandoverRecord? DeliveryHandoverEvidence { get; init; }

    public static string DetermineNextStep(ClaimStatus status) => status switch
    {
        ClaimStatus.PickupPending => "Scan Donor QR Code to complete Pickup",
        ClaimStatus.InTransit => "Scan Beneficiary QR Code to complete Delivery",
        ClaimStatus.Closed => "Task Completed",
        _ => "No action required"
    };
}
