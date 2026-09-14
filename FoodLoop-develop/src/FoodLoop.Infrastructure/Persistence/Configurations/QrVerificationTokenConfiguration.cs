using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace FoodLoop.Infrastructure.Persistence.Configurations;
public sealed class QrVerificationTokenConfiguration : IEntityTypeConfiguration<QrVerificationToken>
{
    public void Configure(EntityTypeBuilder<QrVerificationToken> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.TokenHash).HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne(x => x.DonationClaim).WithMany().HasForeignKey(x => x.DonationClaimId);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CourierUserId);
        b.ToTable("QrVerificationTokens", t => {
            t.HasCheckConstraint("CK_Qr_Purpose", "[Purpose] IN (0,1)");
            t.HasCheckConstraint("CK_Qr_Expiry", "[ExpiresAtUtc] > [CreatedAtUtc]");
        });
    }
}
