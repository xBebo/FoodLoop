using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace FoodLoop.Infrastructure.Persistence.Configurations;
public sealed class FoodDonationConfiguration : IEntityTypeConfiguration<FoodDonation>
{
    public void Configure(EntityTypeBuilder<FoodDonation> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        b.Property(x => x.PickupAddress).HasMaxLength(500).IsRequired();
        b.Property(x => x.StorageInstructions).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Quantity).HasPrecision(12, 3);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.Status, x.ExpiresAtUtc });
        b.HasOne(x => x.DonorOrganization).WithMany().HasForeignKey(x => x.DonorOrganizationId);
        b.HasOne(x => x.FoodCategory).WithMany().HasForeignKey(x => x.FoodCategoryId);
        b.ToTable("FoodDonations", t => {
            t.HasCheckConstraint("CK_Donation_Quantity", "[Quantity] > 0");
            t.HasCheckConstraint("CK_Donation_Dates", "[ExpiresAtUtc] > [PreparedAtUtc]");
            t.HasCheckConstraint("CK_Donation_Unit", "[Unit] IN (0,1,2)");
            t.HasCheckConstraint("CK_Donation_Status", "[Status] IN (0,1,2,3,4,5,6,7,8,9,10)");
        });
    }
}
