using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace FoodLoop.Infrastructure.Persistence;
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<FoodCategory> FoodCategories => Set<FoodCategory>();
    public DbSet<FoodDonation> FoodDonations => Set<FoodDonation>();
    public DbSet<DonationClaim> DonationClaims => Set<DonationClaim>();
    public DbSet<HandoverRecord> HandoverRecords => Set<HandoverRecord>();
    public DbSet<QrVerificationToken> QrVerificationTokens => Set<QrVerificationToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        foreach (var foreignKey in builder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
    }
    private void GuardAuditHistory()
    {
        if (ChangeTracker.Entries<AuditLog>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Audit history cannot be edited or deleted through the application.");
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardAuditHistory();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        GuardAuditHistory();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
