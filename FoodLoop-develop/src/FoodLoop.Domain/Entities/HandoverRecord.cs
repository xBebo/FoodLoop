using FoodLoop.Domain.Common;
using FoodLoop.Domain.Enums;
namespace FoodLoop.Domain.Entities;
public sealed class HandoverRecord : BaseEntity
{
    public Guid DonationClaimId { get; set; }
    public DonationClaim DonationClaim { get; set; } = null!;
    public Guid CourierUserId { get; set; }
    public HandoverType Type { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public string? Notes { get; set; }
}
