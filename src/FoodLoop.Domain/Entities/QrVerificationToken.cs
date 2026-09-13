using FoodLoop.Domain.Common;
using FoodLoop.Domain.Enums;
namespace FoodLoop.Domain.Entities;
public sealed class QrVerificationToken : BaseEntity
{
    public Guid DonationClaimId { get; set; }
    public DonationClaim DonationClaim { get; set; } = null!;
    public Guid CourierUserId { get; set; }
    public QrPurpose Purpose { get; set; }
    // SHA-256 hex hash only. Never persist the raw QR token.
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? UsedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
