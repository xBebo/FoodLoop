using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace FoodLoop.Infrastructure.Persistence.Configurations;
public sealed class HandoverRecordConfiguration : IEntityTypeConfiguration<HandoverRecord>
{
    public void Configure(EntityTypeBuilder<HandoverRecord> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasOne(x => x.DonationClaim).WithMany().HasForeignKey(x => x.DonationClaimId);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CourierUserId);
        b.HasIndex(x => new { x.DonationClaimId, x.Type }).IsUnique();
        b.ToTable("HandoverRecords", t => t.HasCheckConstraint("CK_Handover_Type", "[Type] IN (0,1)"));
    }
}
