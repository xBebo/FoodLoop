using System.Security.Claims;
using FoodLoop.Application.Claims;
using FoodLoop.Application.Identity;
using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Auditing;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Foundation.Tests;
public sealed class ClaimServiceTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    // ---- Wiring: each service gets its own DbContext, real repositories, real CurrentUserService, UnitOfWork and AuditService.
    private static ClaimService Service(ApplicationDbContext db, ClaimsPrincipal principal, IUnitOfWork? unitOfWork = null)
    {
        var clock = new FixedClock(Now);
        var currentUser = new CurrentUserService(new FixedHttpContextAccessor(new DefaultHttpContext { User = principal }), db);
        return new ClaimService(currentUser, new FoodDonationRepository(db, clock), new Repository<Organization>(db),
            new ClaimRepository(db), new AuditService(db, currentUser, clock), unitOfWork ?? new UnitOfWork(db), clock);
    }
    private async Task<CreateClaimResult> CreateAsync(ClaimsPrincipal principal, Guid donationId)
    {
        await using var db = fixture.CreateContext();
        return await Service(db, principal).CreateAsync(donationId, CancellationToken.None);
    }
    private static ClaimsPrincipal Principal(Guid userId, string role = AppRoles.Beneficiary) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, role)], "test"));

    // ---- Seeding
    private static Organization Org(OrganizationType type, OrganizationStatus status = OrganizationStatus.Active) => new()
    { Name = "Test organization", LicenseNumber = Guid.NewGuid().ToString("N"), Type = type, Status = status, Address = "Test address" };
    private async Task<(Guid UserId, Guid? OrganizationId)> SeedUserAsync(Organization? organization)
    {
        await using var db = fixture.CreateContext();
        if (organization is not null) db.Add(organization);
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = Guid.NewGuid().ToString("N"), OrganizationId = organization?.Id };
        db.Add(user); await db.SaveChangesAsync();
        return (user.Id, organization?.Id);
    }
    private Task<(Guid UserId, Guid? OrganizationId)> SeedBeneficiaryAsync(
        OrganizationStatus status = OrganizationStatus.Active, OrganizationType type = OrganizationType.Beneficiary)
        => SeedUserAsync(Org(type, status));
    private async Task<Guid> SeedDonationAsync(DonationStatus status = DonationStatus.Available, DateTimeOffset? expiresAtUtc = null,
        OrganizationStatus donorStatus = OrganizationStatus.Active, OrganizationType donorType = OrganizationType.Donor,
        params ClaimStatus[] existingClaims)
    {
        await using var db = fixture.CreateContext();
        var donation = new FoodDonation
        {
            DonorOrganization = Org(donorType, donorStatus), FoodCategory = new FoodCategory { Name = Guid.NewGuid().ToString("N") },
            Title = "Test food", Description = "Test", Quantity = 10, Unit = QuantityUnit.Meals,
            PreparedAtUtc = Now.AddHours(-3), ExpiresAtUtc = expiresAtUtc ?? Now.AddHours(2),
            PickupAddress = "Test address", StorageInstructions = "Test", Status = status
        };
        db.Add(donation);
        foreach (var claimStatus in existingClaims)
            db.Add(new DonationClaim { FoodDonation = donation, BeneficiaryOrganization = Org(OrganizationType.Beneficiary), Status = claimStatus });
        await db.SaveChangesAsync();
        return donation.Id;
    }

    // ---- Persisted-state verification with a fresh DbContext
    private async Task AssertPersistedAsync(Guid donationId, DonationStatus donationStatus, int claims, int activeClaims, int claimCreatedAudits)
    {
        await using var db = fixture.CreateContext();
        Assert.Equal(donationStatus, (await db.FoodDonations.AsNoTracking().SingleAsync(x => x.Id == donationId)).Status);
        Assert.Equal(claims, await db.DonationClaims.CountAsync(x => x.FoodDonationId == donationId));
        Assert.Equal(activeClaims, await db.DonationClaims.CountAsync(x => x.FoodDonationId == donationId && (int)x.Status <= (int)ClaimStatus.Delivered));
        Assert.Equal(claimCreatedAudits, await ClaimCreatedAudits(db, donationId).CountAsync());
    }
    private static IQueryable<AuditLog> ClaimCreatedAudits(ApplicationDbContext db, Guid donationId)
        => db.AuditLogs.Where(x => x.Action == "ClaimCreated" && x.Details!.Contains("DonationId=" + donationId));
    private Task AssertRejectedWithoutWritesAsync(Guid donationId, DonationStatus donationStatus = DonationStatus.Available)
        => AssertPersistedAsync(donationId, donationStatus, claims: 0, activeClaims: 0, claimCreatedAudits: 0);

    // ---- Success
    [Fact]
    public async Task Successful_claim_books_donation_for_current_beneficiary()
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync(); var donationId = await SeedDonationAsync();
        var result = await CreateAsync(Principal(userId), donationId);
        Assert.Equal(CreateClaimOutcome.Created, result.Outcome); Assert.NotNull(result.ClaimId);
        await AssertPersistedAsync(donationId, DonationStatus.Claimed, claims: 1, activeClaims: 1, claimCreatedAudits: 1);
        await using var db = fixture.CreateContext();
        var claim = await db.DonationClaims.AsNoTracking().SingleAsync(x => x.Id == result.ClaimId);
        Assert.Equal(donationId, claim.FoodDonationId); Assert.Equal(organizationId, claim.BeneficiaryOrganizationId);
        Assert.Equal(ClaimStatus.Booked, claim.Status); Assert.Equal(Now, claim.CreatedAtUtc);
        Assert.Null(claim.AssignedCourierUserId);
    }
    [Fact]
    public async Task Successful_claim_writes_exactly_one_ClaimCreated_audit()
    {
        var (userId, _) = await SeedBeneficiaryAsync(); var donationId = await SeedDonationAsync();
        var result = await CreateAsync(Principal(userId), donationId);
        await using var db = fixture.CreateContext();
        var audit = Assert.Single(await db.AuditLogs.AsNoTracking().Where(x => x.EntityId == result.ClaimId || x.EntityId == donationId).ToListAsync());
        Assert.Equal("ClaimCreated", audit.Action); Assert.Equal(nameof(DonationClaim), audit.EntityType);
        Assert.Equal(result.ClaimId, audit.EntityId); Assert.Equal(userId, audit.ActorUserId); Assert.Equal(Now, audit.CreatedAtUtc);
        Assert.Contains($"DonationId={donationId}", audit.Details); Assert.Contains("DonationStatus=Available->Claimed", audit.Details);
        Assert.Equal(1, await ClaimCreatedAudits(db, donationId).CountAsync());
    }

    // ---- Caller rejections
    [Fact]
    public async Task Unauthenticated_user_is_rejected()
    {
        var donationId = await SeedDonationAsync();
        var result = await CreateAsync(new ClaimsPrincipal(new ClaimsIdentity()), donationId);
        Assert.Equal(CreateClaimOutcome.Unauthenticated, result.Outcome); Assert.Null(result.ClaimId);
        await AssertRejectedWithoutWritesAsync(donationId);
    }
    [Theory]
    [InlineData(AppRoles.Donor)]
    [InlineData(AppRoles.Courier)]
    [InlineData(AppRoles.Admin)]
    public async Task Wrong_role_is_rejected(string role)
    {
        var (userId, _) = await SeedBeneficiaryAsync(); var donationId = await SeedDonationAsync();
        Assert.Equal(CreateClaimOutcome.Forbidden, (await CreateAsync(Principal(userId, role), donationId)).Outcome);
        await AssertRejectedWithoutWritesAsync(donationId);
    }
    [Fact]
    public async Task Beneficiary_without_linked_organization_is_rejected()
    {
        var (userId, _) = await SeedUserAsync(null); var donationId = await SeedDonationAsync();
        Assert.Equal(CreateClaimOutcome.OrganizationNotActive, (await CreateAsync(Principal(userId), donationId)).Outcome);
        await AssertRejectedWithoutWritesAsync(donationId);
    }
    [Theory]
    [InlineData(OrganizationStatus.Pending)]
    [InlineData(OrganizationStatus.Rejected)]
    [InlineData(OrganizationStatus.Suspended)]
    public async Task Beneficiary_organization_not_active_is_rejected(OrganizationStatus status)
    {
        var (userId, _) = await SeedBeneficiaryAsync(status); var donationId = await SeedDonationAsync();
        Assert.Equal(CreateClaimOutcome.OrganizationNotActive, (await CreateAsync(Principal(userId), donationId)).Outcome);
        await AssertRejectedWithoutWritesAsync(donationId);
    }
    [Fact]
    public async Task Beneficiary_role_linked_to_donor_organization_is_rejected()
    {
        var (userId, _) = await SeedBeneficiaryAsync(type: OrganizationType.Donor); var donationId = await SeedDonationAsync();
        Assert.Equal(CreateClaimOutcome.OrganizationNotActive, (await CreateAsync(Principal(userId), donationId)).Outcome);
        await AssertRejectedWithoutWritesAsync(donationId);
    }

    // ---- Donation rejections
    [Fact]
    public async Task Missing_donation_is_rejected()
    {
        var (userId, _) = await SeedBeneficiaryAsync(); var missingId = Guid.NewGuid();
        Assert.Equal(CreateClaimOutcome.DonationNotFound, (await CreateAsync(Principal(userId), missingId)).Outcome);
        await using var db = fixture.CreateContext();
        Assert.False(await db.DonationClaims.AnyAsync(x => x.FoodDonationId == missingId));
        Assert.False(await ClaimCreatedAudits(db, missingId).AnyAsync());
    }
    [Fact]
    public async Task Expired_donation_is_rejected_and_left_unchanged()
    {
        var (userId, _) = await SeedBeneficiaryAsync(); var donationId = await SeedDonationAsync(expiresAtUtc: Now.AddMinutes(-1));
        Assert.Equal(CreateClaimOutcome.DonationExpired, (await CreateAsync(Principal(userId), donationId)).Outcome);
        await AssertRejectedWithoutWritesAsync(donationId, DonationStatus.Available);
    }
    [Fact]
    public async Task Donation_expiring_exactly_now_is_rejected()
    {
        var (userId, _) = await SeedBeneficiaryAsync(); var donationId = await SeedDonationAsync(expiresAtUtc: Now);
        Assert.Equal(CreateClaimOutcome.DonationExpired, (await CreateAsync(Principal(userId), donationId)).Outcome);
        await AssertRejectedWithoutWritesAsync(donationId, DonationStatus.Available);
    }
    [Theory]
    [InlineData(DonationStatus.Draft)]
    [InlineData(DonationStatus.Claimed)]
    [InlineData(DonationStatus.Delivered)]
    [InlineData(DonationStatus.Closed)]
    [InlineData(DonationStatus.Expired)]
    [InlineData(DonationStatus.Cancelled)]
    public async Task Donation_not_available_is_rejected(DonationStatus status)
    {
        var (userId, _) = await SeedBeneficiaryAsync(); var donationId = await SeedDonationAsync(status);
        Assert.Equal(CreateClaimOutcome.DonationNotAvailable, (await CreateAsync(Principal(userId), donationId)).Outcome);
        await AssertRejectedWithoutWritesAsync(donationId, status);
    }
    [Theory]
    [InlineData(OrganizationStatus.Pending)]
    [InlineData(OrganizationStatus.Rejected)]
    [InlineData(OrganizationStatus.Suspended)]
    public async Task Donor_organization_not_active_is_rejected(OrganizationStatus status)
    {
        var (userId, _) = await SeedBeneficiaryAsync(); var donationId = await SeedDonationAsync(donorStatus: status);
        Assert.Equal(CreateClaimOutcome.DonorOrganizationNotActive, (await CreateAsync(Principal(userId), donationId)).Outcome);
        await AssertRejectedWithoutWritesAsync(donationId);
    }
    [Fact]
    public async Task Donation_linked_to_beneficiary_type_organization_is_rejected()
    {
        var (userId, _) = await SeedBeneficiaryAsync(); var donationId = await SeedDonationAsync(donorType: OrganizationType.Beneficiary);
        Assert.Equal(CreateClaimOutcome.DonorOrganizationNotActive, (await CreateAsync(Principal(userId), donationId)).Outcome);
        await AssertRejectedWithoutWritesAsync(donationId);
    }

    // ---- Double booking protection
    [Fact]
    public async Task Sequential_duplicate_claim_creates_only_one_active_claim()
    {
        var (first, _) = await SeedBeneficiaryAsync(); var (second, _) = await SeedBeneficiaryAsync(); var donationId = await SeedDonationAsync();
        Assert.Equal(CreateClaimOutcome.Created, (await CreateAsync(Principal(first), donationId)).Outcome);
        Assert.Equal(CreateClaimOutcome.DonationNotAvailable, (await CreateAsync(Principal(second), donationId)).Outcome);
        Assert.Equal(CreateClaimOutcome.DonationNotAvailable, (await CreateAsync(Principal(first), donationId)).Outcome);
        await AssertPersistedAsync(donationId, DonationStatus.Claimed, claims: 1, activeClaims: 1, claimCreatedAudits: 1);
    }
    [Fact]
    public async Task Existing_active_claim_on_available_donation_returns_conflict()
    {
        var (userId, _) = await SeedBeneficiaryAsync(); var donationId = await SeedDonationAsync(existingClaims: ClaimStatus.Booked);
        await using var db = fixture.CreateContext();
        var result = await Service(db, Principal(userId)).CreateAsync(donationId, CancellationToken.None);
        Assert.Equal(CreateClaimOutcome.Conflict, result.Outcome); Assert.Null(result.ClaimId);
        // Rejected by the active-claim pre-check: nothing was staged, so no save was attempted (a failed save would leave Added entries).
        Assert.All(db.ChangeTracker.Entries(), x => Assert.Equal(EntityState.Unchanged, x.State));
        await AssertPersistedAsync(donationId, DonationStatus.Available, claims: 1, activeClaims: 1, claimCreatedAudits: 0);
    }
    [Fact]
    public async Task Conflict_leaves_no_second_claim_no_orphan_audit_and_no_status_change()
    {
        var (userId, _) = await SeedBeneficiaryAsync(); var donationId = await SeedDonationAsync(existingClaims: ClaimStatus.Booked);
        Assert.Equal(CreateClaimOutcome.Conflict, (await CreateAsync(Principal(userId), donationId)).Outcome);
        await AssertPersistedAsync(donationId, DonationStatus.Available, claims: 1, activeClaims: 1, claimCreatedAudits: 0);
        await using var db = fixture.CreateContext();
        Assert.False(await db.AuditLogs.AnyAsync(x => x.ActorUserId == userId));
    }
    [Fact]
    public async Task Historical_cancelled_claim_does_not_block_a_new_claim()
    {
        var (userId, _) = await SeedBeneficiaryAsync(); var donationId = await SeedDonationAsync(existingClaims: ClaimStatus.Cancelled);
        var result = await CreateAsync(Principal(userId), donationId);
        Assert.Equal(CreateClaimOutcome.Created, result.Outcome);
        await AssertPersistedAsync(donationId, DonationStatus.Claimed, claims: 2, activeClaims: 1, claimCreatedAudits: 1);
        await using var db = fixture.CreateContext();
        Assert.Equal(result.ClaimId, (await new ClaimRepository(db).GetActiveForDonationAsync(donationId))!.Id);
    }
    [Fact]
    public async Task Concurrent_claims_from_two_beneficiaries_produce_one_winner_and_one_conflict()
    {
        var (userA, orgA) = await SeedBeneficiaryAsync(); var (userB, orgB) = await SeedBeneficiaryAsync(); var donationId = await SeedDonationAsync();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stagedA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stagedB = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var dbA = fixture.CreateContext(); await using var dbB = fixture.CreateContext();
        var requestA = Service(dbA, Principal(userA), new GatedUnitOfWork(new UnitOfWork(dbA), stagedA, release.Task)).CreateAsync(donationId, CancellationToken.None);
        var requestB = Service(dbB, Principal(userB), new GatedUnitOfWork(new UnitOfWork(dbB), stagedB, release.Task)).CreateAsync(donationId, CancellationToken.None);

        // Both requests have passed every validation, including the active-claim pre-check, and staged claim + status + audit; neither has saved.
        await Task.WhenAll(stagedA.Task, stagedB.Task).WaitAsync(TimeSpan.FromSeconds(30));
        foreach (var db in new[] { dbA, dbB })
        {
            Assert.Equal(EntityState.Modified, db.ChangeTracker.Entries<FoodDonation>().Single().State);
            Assert.Equal(EntityState.Added, db.ChangeTracker.Entries<DonationClaim>().Single().State);
            Assert.Equal(EntityState.Added, db.ChangeTracker.Entries<AuditLog>().Single().State);
        }
        Assert.Equal(dbA.ChangeTracker.Entries<FoodDonation>().Single().Entity.RowVersion, dbB.ChangeTracker.Entries<FoodDonation>().Single().Entity.RowVersion);
        release.SetResult();
        var results = await Task.WhenAll(requestA, requestB).WaitAsync(TimeSpan.FromSeconds(30));

        var winner = Assert.Single(results, x => x.Outcome == CreateClaimOutcome.Created);
        Assert.Single(results, x => x.Outcome == CreateClaimOutcome.Conflict);
        await AssertPersistedAsync(donationId, DonationStatus.Claimed, claims: 1, activeClaims: 1, claimCreatedAudits: 1);
        await using var verify = fixture.CreateContext();
        var claim = await verify.DonationClaims.AsNoTracking().SingleAsync(x => x.FoodDonationId == donationId);
        Assert.Equal(winner.ClaimId, claim.Id);
        Assert.Equal(results[0] == winner ? orgA : orgB, claim.BeneficiaryOrganizationId);
        Assert.Equal(claim.Id, (await ClaimCreatedAudits(verify, donationId).SingleAsync()).EntityId);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
    private sealed class FixedHttpContextAccessor(HttpContext context) : IHttpContextAccessor
    {
        // The framework accessor stores HttpContext in a static AsyncLocal; concurrent fake requests need independent values.
        public HttpContext? HttpContext { get => context; set { } }
    }
    private sealed class GatedUnitOfWork(IUnitOfWork inner, TaskCompletionSource staged, Task release) : IUnitOfWork
    {
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            staged.SetResult();
            await release;
            return await inner.SaveChangesAsync(cancellationToken);
        }
        public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => inner.BeginTransactionAsync(cancellationToken);
    }
}
