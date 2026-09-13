using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace FoodLoop.Infrastructure.Persistence.Configurations;
public sealed class DonationClaimConfiguration : IEntityTypeConfiguration<DonationClaim>
{
    public void Configure(EntityTypeBuilder<DonationClaim> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.FailureReason).HasMaxLength(1000);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne(x => x.FoodDonation).WithMany().HasForeignKey(x => x.FoodDonationId);
        b.HasOne(x => x.BeneficiaryOrganization).WithMany().HasForeignKey(x => x.BeneficiaryOrganizationId);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AssignedCourierUserId);
        // Booked through Delivered retain the reservation. Keep this aligned with ClaimStatus.
        b.HasIndex(x => x.FoodDonationId).IsUnique().HasDatabaseName("UX_Claim_ActiveDonation")
            .HasFilter("[Status] IN (0,1,2,3,4)");
        b.ToTable("DonationClaims", t => t.HasCheckConstraint("CK_Claim_Status", "[Status] IN (0,1,2,3,4,5,6,7)"));
    }
}
