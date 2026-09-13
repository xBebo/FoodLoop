using FoodLoop.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace FoodLoop.Infrastructure.Persistence.Configurations;
public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.HasIndex(x => x.NormalizedEmail).IsUnique();
        b.Property(x => x.DisplayName).HasMaxLength(150).IsRequired();
        b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId);
    }
}
