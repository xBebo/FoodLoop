using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace FoodLoop.Infrastructure.Persistence.Configurations;
public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.LicenseNumber).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.LicenseNumber).IsUnique();
        b.Property(x => x.Address).HasMaxLength(500).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.ToTable("Organizations", t => { t.HasCheckConstraint("CK_Organization_Type", "[Type] IN (0,1)"); t.HasCheckConstraint("CK_Organization_Status", "[Status] IN (0,1,2,3)"); });
    }
}
