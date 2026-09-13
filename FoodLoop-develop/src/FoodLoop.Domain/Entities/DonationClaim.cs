using FoodLoop.Domain.Common;
using FoodLoop.Domain.Enums;
namespace FoodLoop.Domain.Entities;
public sealed class DonationClaim : BaseEntity
{
    public Guid FoodDonationId { get; set; }
    public FoodDonation FoodDonation { get; set; } = null!;
    public Guid BeneficiaryOrganizationId { get; set; }
    public Organization BeneficiaryOrganization { get; set; } = null!;
    public Guid? AssignedCourierUserId { get; set; }
    public ClaimStatus Status { get; set; } = ClaimStatus.Booked;
    public string? FailureReason { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
