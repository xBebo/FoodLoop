using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace FoodLoop.Infrastructure.Persistence.Configurations;
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        b.Property(x => x.Details).HasMaxLength(2000);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId);
        b.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAtUtc });
    }
}
