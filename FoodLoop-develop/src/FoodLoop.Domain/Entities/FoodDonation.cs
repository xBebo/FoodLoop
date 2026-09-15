using FoodLoop.Domain.Common;
using FoodLoop.Domain.Enums;
namespace FoodLoop.Domain.Entities;
public sealed class FoodDonation : BaseEntity
{
    public Guid DonorOrganizationId { get; set; }
    public Organization DonorOrganization { get; set; } = null!;
    public Guid FoodCategoryId { get; set; }
    public FoodCategory FoodCategory { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public QuantityUnit Unit { get; set; }
    public DateTimeOffset PreparedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public string StorageInstructions { get; set; } = string.Empty;
    public string PickupAddress { get; set; } = string.Empty;
    public DonationStatus Status { get; set; } = DonationStatus.Draft;
    public byte[] RowVersion { get; set; } = [];
}
