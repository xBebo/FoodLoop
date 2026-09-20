using System;
using System.Threading;
using System.Threading.Tasks;
using FoodLoop.Application.Interfaces.Identity;
using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.Courier;

public sealed class TaskDetailsService(
    IRepository<DonationClaim> claimRepository,
    IRepository<FoodDonation> donationRepository,
    ICurrentUserService currentUser)
{
    public async Task<CourierTaskDetailsDto?> GetTaskDetailsAsync(Guid claimId, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (userId == null) return null;

        var claim = await claimRepository.GetByIdAsync(claimId, ct);
        if (claim == null) return null;

        var donation = await donationRepository.GetByIdAsync(claim.FoodDonationId, ct);

        return new CourierTaskDetailsDto
        {
            ClaimId = claim.Id,
            DonationTitle = donation?.Title ?? "Donation Task",
            DonorOrganizationName = "Donor Organization",
            BeneficiaryOrganizationName = "Beneficiary Organization",
            PickupAddress = donation?.PickupAddress ?? "Address Not Specified",
            ExpiryDate = DateTimeOffset.UtcNow,
            Status = claim.Status,
            NextStep = DetermineNextStep(claim.Status)
        };
    }

    private static string DetermineNextStep(ClaimStatus status) => status switch
    {
        ClaimStatus.PickupPending => "Scan Donor QR Code to complete Pickup",
        ClaimStatus.InTransit => "Scan Beneficiary QR Code to complete Delivery",
        ClaimStatus.Closed => "Task Completed",
        _ => "No action required"
    };
}

public class CourierTaskDetailsDto
{
    public Guid ClaimId { get; set; }
    public string DonationTitle { get; set; } = string.Empty;
    public string DonorOrganizationName { get; set; } = string.Empty;
    public string BeneficiaryOrganizationName { get; set; } = string.Empty;
    public string PickupAddress { get; set; } = string.Empty;
    public DateTimeOffset ExpiryDate { get; set; }
    public ClaimStatus Status { get; set; }
    public string NextStep { get; set; } = string.Empty;
    public HandoverRecord? PickupHandoverEvidence { get; set; }
    public HandoverRecord? DeliveryHandoverEvidence { get; set; }
}