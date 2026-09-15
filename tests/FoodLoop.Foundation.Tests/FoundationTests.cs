using System.Security.Claims;
using FoodLoop.Application.Donations;
using FoodLoop.Application.Exceptions;
using FoodLoop.Application.Identity;
using FoodLoop.Application.Interfaces.Identity;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure;
using FoodLoop.Infrastructure.Auditing;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FoodLoop.Foundation.Tests;
public sealed class FoundationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static Organization Org(OrganizationType type = OrganizationType.Donor) => new()
    { Name = "Test organization", LicenseNumber = Guid.NewGuid().ToString("N"), Type = type, Status = OrganizationStatus.Active, Address = "Test address" };
    private static FoodDonation Donation() => new()
    {
        DonorOrganization = Org(), FoodCategory = new FoodCategory { Name = Guid.NewGuid().ToString("N") },
        Title = "Test food", Description = "Test", Quantity = 10, Unit = QuantityUnit.Meals,
        PreparedAtUtc = DateTimeOffset.UtcNow.AddHours(-1), ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2),
        PickupAddress = "Test address", StorageInstructions = "Test", Status = DonationStatus.Available
    };
    private async Task<Guid> CreateDonationAsync()
    {
        await using var db = fixture.CreateContext(); var donation = Donation();
        db.Add(donation); await db.SaveChangesAsync(); return donation.Id;
    }
    private static DonationClaim Claim(Guid donationId) => new()
    { FoodDonationId = donationId, BeneficiaryOrganization = Org(OrganizationType.Beneficiary) };

    [Fact]
    public async Task Migration_matches_model_and_is_repeatable()
    {
        await using var db = fixture.CreateContext();
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        await db.Database.MigrateAsync();
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Database_rejects_nonpositive_quantity(int quantity)
    {
        await using var db = fixture.CreateContext(); var donation = Donation(); donation.Quantity = quantity;
        db.Add(donation); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task Database_rejects_invalid_expiry_interval()
    {
        await using var db = fixture.CreateContext(); var donation = Donation(); donation.ExpiresAtUtc = donation.PreparedAtUtc;
        db.Add(donation); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task Duplicate_license_is_rejected()
    {
        await using var db = fixture.CreateContext(); var first = Org(); var second = Org(); second.LicenseNumber = first.LicenseNumber;
        db.AddRange(first, second);
        await Assert.ThrowsAsync<PersistenceConflictException>(() => new UnitOfWork(db).SaveChangesAsync());
    }
    [Fact]
    public async Task Filtered_unique_index_blocks_second_active_claim_without_donation_update()
    {
        var donationId = await CreateDonationAsync();
        await using (var first = fixture.CreateContext()) { first.Add(Claim(donationId)); await first.SaveChangesAsync(); }
        await using var second = fixture.CreateContext(); second.Add(Claim(donationId));
        await Assert.ThrowsAsync<PersistenceConflictException>(() => new UnitOfWork(second).SaveChangesAsync());
        await using var verify = fixture.CreateContext();
        Assert.Equal(1, await verify.DonationClaims.CountAsync(x => x.FoodDonationId == donationId));
    }
    [Fact]
    public async Task Cancelled_claim_releases_unique_slot_and_active_query_ignores_history()
    {
        var donationId = await CreateDonationAsync();
        await using var db = fixture.CreateContext(); var previous = Claim(donationId); previous.Status = ClaimStatus.Cancelled;
        var current = Claim(donationId); db.AddRange(previous, current); await db.SaveChangesAsync();
        Assert.Equal(current.Id, (await new ClaimRepository(db).GetActiveForDonationAsync(donationId))!.Id);
    }
    [Fact]
    public async Task Concurrent_donation_updates_allow_one_claim_and_one_audit_only()
    {
        var donationId = await CreateDonationAsync();
        await using var first = fixture.CreateContext(); await using var second = fixture.CreateContext();
        var a = await first.FoodDonations.SingleAsync(x => x.Id == donationId);
        var b = await second.FoodDonations.SingleAsync(x => x.Id == donationId);
        // Both read the same rowversion before either writes: deterministic competing snapshots.
        Assert.Equal(a.RowVersion, b.RowVersion);
        a.Status = b.Status = DonationStatus.Claimed;
        first.Add(Claim(donationId)); second.Add(Claim(donationId));
        new AuditService(first, new AnonymousUser(), TimeProvider.System).Record("Claimed", "FoodDonation", donationId);
        new AuditService(second, new AnonymousUser(), TimeProvider.System).Record("Claimed", "FoodDonation", donationId);
        async Task<bool> TrySave(ApplicationDbContext context)
        {
            try { await new UnitOfWork(context).SaveChangesAsync(); return true; }
            catch (PersistenceConflictException) { return false; }
        }
        var results = await Task.WhenAll(TrySave(first), TrySave(second));
        Assert.Single(results, x => x); Assert.Single(results, x => !x);
        await using var verify = fixture.CreateContext();
        Assert.Equal(1, await verify.DonationClaims.CountAsync(x => x.FoodDonationId == donationId));
        Assert.Equal(1, await verify.AuditLogs.CountAsync(x => x.EntityId == donationId));
        Assert.Equal(DonationStatus.Claimed, (await verify.FoodDonations.FindAsync(donationId))!.Status);
    }
    [Fact]
    public async Task Explicit_transaction_rollback_removes_staged_business_and_audit_changes()
    {
        var donationId = await CreateDonationAsync(); await using var db = fixture.CreateContext();
        var uow = new UnitOfWork(db); await using var transaction = await uow.BeginTransactionAsync();
        var donation = (await db.FoodDonations.FindAsync(donationId))!; donation.Status = DonationStatus.Claimed;
        db.Add(Claim(donationId)); new AuditService(db, new AnonymousUser(), TimeProvider.System).Record("Claimed", "FoodDonation", donationId);
        await uow.SaveChangesAsync(); await transaction.RollbackAsync();
        await using var verify = fixture.CreateContext();
        Assert.False(await verify.AuditLogs.AnyAsync(x => x.EntityId == donationId));
        Assert.False(await verify.DonationClaims.AnyAsync(x => x.FoodDonationId == donationId));
        Assert.Equal(DonationStatus.Available, (await verify.FoodDonations.FindAsync(donationId))!.Status);
    }
    [Fact]
    public async Task Marketplace_excludes_expired_and_suspended_donor_listings()
    {
        await using var db = fixture.CreateContext();
        var good = Donation(); good.ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(1);
        var expired = Donation(); expired.ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        var suspended = Donation(); suspended.DonorOrganization.Status = OrganizationStatus.Suspended;
        db.AddRange(good, expired, suspended); await db.SaveChangesAsync();
        var result = await new FoodDonationRepository(db, TimeProvider.System).GetAvailableAsync(1, 100);
        Assert.Contains(result, x => x.Id == good.Id);
        Assert.DoesNotContain(result, x => x.Id == expired.Id || x.Id == suspended.Id);
    }
    [Fact]
    public async Task Foreign_keys_prevent_deleting_an_organization_with_donations()
    {
        var id = await CreateDonationAsync(); await using var db = fixture.CreateContext();
        var donation = await db.FoodDonations.AsNoTracking().SingleAsync(x => x.Id == id);
        db.Organizations.Remove((await db.Organizations.FindAsync(donation.DonorOrganizationId))!);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task Audit_cannot_be_edited_or_deleted_using_SaveChanges()
    {
        await using var db = fixture.CreateContext(); var entry = new AuditLog { Action = "Created", EntityType = "Test", EntityId = Guid.NewGuid() };
        db.Add(entry); await db.SaveChangesAsync(); entry.Action = "Changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.Entry(entry).State = EntityState.Unchanged; db.Remove(entry);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }
    [Fact]
    public async Task Handover_is_unique_per_claim_and_type()
    {
        var donationId = await CreateDonationAsync(); await using var db = fixture.CreateContext();
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = Guid.NewGuid().ToString("N") }; var claim = Claim(donationId);
        db.AddRange(user, claim); await db.SaveChangesAsync();
        db.Add(new HandoverRecord { DonationClaimId = claim.Id, CourierUserId = user.Id, Type = HandoverType.Pickup, CompletedAtUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        db.Add(new HandoverRecord { DonationClaimId = claim.Id, CourierUserId = user.Id, Type = HandoverType.Pickup, CompletedAtUtc = DateTimeOffset.UtcNow });
        await Assert.ThrowsAsync<PersistenceConflictException>(() => new UnitOfWork(db).SaveChangesAsync());
    }
    [Fact]
    public async Task Qr_rowversion_rejects_a_second_consumer_of_the_same_snapshot()
    {
        var donationId = await CreateDonationAsync(); var tokenId = Guid.NewGuid();
        await using (var setup = fixture.CreateContext())
        {
            var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = Guid.NewGuid().ToString("N") }; var claim = Claim(donationId);
            setup.AddRange(user, claim); await setup.SaveChangesAsync();
            setup.Add(new QrVerificationToken { Id = tokenId, DonationClaimId = claim.Id, CourierUserId = user.Id,
                TokenHash = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)), ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5) });
            await setup.SaveChangesAsync();
        }
        await using var first = fixture.CreateContext(); await using var second = fixture.CreateContext();
        var a = (await first.QrVerificationTokens.FindAsync(tokenId))!; var b = (await second.QrVerificationTokens.FindAsync(tokenId))!;
        a.UsedAtUtc = b.UsedAtUtc = DateTimeOffset.UtcNow;
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<PersistenceConflictException>(() => new UnitOfWork(second).SaveChangesAsync());
    }
    [Fact]
    public async Task Identity_seeding_is_repeatable_and_current_user_reads_organization_from_database()
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddInfrastructure(fixture.ConnectionString);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>();
        var password = "Test!" + Guid.NewGuid().ToString("N") + "7";
        await seeder.SeedAsync(password); await seeder.SeedAsync(password);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(4, await db.Roles.CountAsync());
        Assert.Equal(6, await db.Users.CountAsync(x => x.Email != null && x.Email.EndsWith("@foodloop.test")));
        var user = await db.Users.SingleAsync(x => x.Email == "donor@foodloop.test");
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True(await userManager.CheckPasswordAsync(user, password));
        Assert.True(await userManager.IsInRoleAsync(user, AppRoles.Donor));
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Role, AppRoles.Donor)], "test")) };
        var current = scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
        Assert.Equal(user.Id, current.UserId); Assert.True(current.IsInRole(AppRoles.Donor));
        Assert.Equal(user.OrganizationId, await current.GetOrganizationIdAsync());
    }

    [Fact]
    public async Task Donation_feature_creates_draft_and_publishes_valid_active_donor_listing()
    {
        await using var db = fixture.CreateContext();
        var organization = Org(OrganizationType.Donor);
        var category = new FoodCategory { Name = "Donation test " + Guid.NewGuid().ToString("N") };
        db.AddRange(organization, category);
        await db.SaveChangesAsync();

        var currentUser = new TestUser(organization.Id, AppRoles.Donor);
        var service = new DonationService(
            currentUser,
            new FoodDonationRepository(db, TimeProvider.System),
            new FoodCategoryRepository(db),
            new Repository<Organization>(db),
            new UnitOfWork(db),
            new AuditService(db, currentUser, TimeProvider.System),
            TimeProvider.System);

        var create = await service.CreateAsync(new CreateDonationRequest(
            category.Id,
            "50 Rice Meals",
            "Freshly prepared meals",
            50,
            QuantityUnit.Meals,
            DateTimeOffset.UtcNow.AddMinutes(-10),
            DateTimeOffset.UtcNow.AddHours(2),
            "Keep covered",
            "New Damietta"));

        Assert.True(create.Succeeded);
        Assert.NotNull(create.DonationId);
        Assert.Equal(DonationStatus.Draft, (await db.FoodDonations.FindAsync(create.DonationId!.Value))!.Status);

        var publish = await service.PublishAsync(create.DonationId.Value);
        Assert.True(publish.Succeeded);
        Assert.Equal(DonationStatus.Available, (await db.FoodDonations.FindAsync(create.DonationId.Value))!.Status);
        Assert.Equal(2, await db.AuditLogs.CountAsync(x => x.EntityId == create.DonationId.Value));
    }

    [Fact]
    public async Task Donation_feature_blocks_pending_donor_and_expired_publish()
    {
        await using var db = fixture.CreateContext();
        var pending = Org(OrganizationType.Donor); pending.Status = OrganizationStatus.Pending;
        var active = Org(OrganizationType.Donor);
        var category = new FoodCategory { Name = "Donation validation " + Guid.NewGuid().ToString("N") };
        db.AddRange(pending, active, category);
        await db.SaveChangesAsync();

        DonationService Build(ICurrentUserService user) => new(
            user,
            new FoodDonationRepository(db, TimeProvider.System),
            new FoodCategoryRepository(db),
            new Repository<Organization>(db),
            new UnitOfWork(db),
            new AuditService(db, user, TimeProvider.System),
            TimeProvider.System);

        var pendingResult = await Build(new TestUser(pending.Id, AppRoles.Donor)).CreateAsync(new CreateDonationRequest(
            category.Id, "Pending donor food", "", 5, QuantityUnit.Meals,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), "", "Address"));
        Assert.False(pendingResult.Succeeded);

        var expiredDraft = new FoodDonation
        {
            DonorOrganizationId = active.Id,
            FoodCategoryId = category.Id,
            Title = "Expired draft",
            Description = "",
            Quantity = 5,
            Unit = QuantityUnit.Meals,
            PreparedAtUtc = DateTimeOffset.UtcNow.AddHours(-2),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(-1),
            PickupAddress = "Address",
            StorageInstructions = "",
            Status = DonationStatus.Draft
        };
        db.Add(expiredDraft);
        await db.SaveChangesAsync();

        var publish = await Build(new TestUser(active.Id, AppRoles.Donor)).PublishAsync(expiredDraft.Id);
        Assert.False(publish.Succeeded);
        Assert.Equal(DonationStatus.Draft, expiredDraft.Status);
    }

    private sealed class AnonymousUser : ICurrentUserService
    {
        public Guid? UserId => null;
        public bool IsAuthenticated => false;
        public bool IsInRole(string role) => false;
        public Task<Guid?> GetOrganizationIdAsync(CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(null);
    }

    private sealed class TestUser(Guid organizationId, string role) : ICurrentUserService
    {
        public Guid? UserId => null;
        public bool IsAuthenticated => true;
        public bool IsInRole(string requestedRole) => string.Equals(role, requestedRole, StringComparison.Ordinal);
        public Task<Guid?> GetOrganizationIdAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<Guid?>(organizationId);
    }
}
