using System.Security.Claims;
using FoodLoop.Application.Claims;
using FoodLoop.Application.Courier;
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
// Claim cancellation (TASKS-02). Shares the ClaimServiceTests database, wiring and seeding helpers.
public sealed partial class ClaimServiceTests
{
    private async Task<CancelClaimResult> CancelAsync(ClaimsPrincipal principal, Guid claimId)
    {
        await using var db = fixture.CreateContext();
        return await Service(db, principal).CancelAsync(claimId, CancellationToken.None);
    }
    private async Task<(Guid ClaimId, Guid DonationId)> SeedClaimForCancelAsync(Guid beneficiaryOrganizationId,
        ClaimStatus status = ClaimStatus.Booked, DonationStatus donationStatus = DonationStatus.Claimed, DateTimeOffset? expiresAtUtc = null,
        OrganizationStatus donorStatus = OrganizationStatus.Active, OrganizationType donorType = OrganizationType.Donor, Guid? courierUserId = null)
    {
        await using var db = fixture.CreateContext();
        var claim = new DonationClaim
        {
            BeneficiaryOrganizationId = beneficiaryOrganizationId, Status = status, AssignedCourierUserId = courierUserId, CreatedAtUtc = Now.AddMinutes(-10),
            FoodDonation = new FoodDonation
            {
                DonorOrganization = Org(donorType, donorStatus), FoodCategory = new FoodCategory { Name = Guid.NewGuid().ToString("N") },
                Title = "Cancellable food", Description = "Test", Quantity = 5, Unit = QuantityUnit.Meals, PreparedAtUtc = Now.AddHours(-3),
                ExpiresAtUtc = expiresAtUtc ?? Now.AddHours(2), PickupAddress = "Test address", StorageInstructions = "Test", Status = donationStatus
            }
        };
        db.Add(claim); await db.SaveChangesAsync();
        return (claim.Id, claim.FoodDonationId);
    }

    // ---- Persisted-state verification with a fresh DbContext
    private sealed record PersistedState(ClaimStatus ClaimStatus, Guid? CourierUserId, string ClaimRowVersion,
        DonationStatus DonationStatus, string DonationRowVersion, int ClaimAudits, int HandoverArtifacts);
    private async Task<PersistedState> ReadStateAsync(Guid claimId)
    {
        await using var db = fixture.CreateContext();
        var claim = await db.DonationClaims.AsNoTracking().Include(x => x.FoodDonation).SingleAsync(x => x.Id == claimId);
        return new(claim.Status, claim.AssignedCourierUserId, Convert.ToHexString(claim.RowVersion),
            claim.FoodDonation.Status, Convert.ToHexString(claim.FoodDonation.RowVersion),
            await db.AuditLogs.CountAsync(x => x.EntityId == claimId || x.EntityId == claim.FoodDonationId),
            await db.QrVerificationTokens.CountAsync(x => x.DonationClaimId == claimId) + await db.HandoverRecords.CountAsync(x => x.DonationClaimId == claimId));
    }
    // Unchanged RowVersions prove no UPDATE reached the claim or donation.
    private async Task AssertRejectedAsync(ClaimsPrincipal principal, Guid claimId, CancelClaimOutcome expected)
    {
        var before = await ReadStateAsync(claimId);
        Assert.Equal(new CancelClaimResult(expected), await CancelAsync(principal, claimId));
        Assert.Equal(before, await ReadStateAsync(claimId));
    }
    private async Task AssertCancelledAsync(Guid claimId, Guid donationId, Guid actorUserId, DonationStatus donationStatus)
    {
        await using var db = fixture.CreateContext();
        var claim = await db.DonationClaims.AsNoTracking().Include(x => x.FoodDonation).SingleAsync(x => x.Id == claimId);
        Assert.Equal(ClaimStatus.Cancelled, claim.Status); Assert.Null(claim.AssignedCourierUserId);
        Assert.Equal(donationStatus, claim.FoodDonation.Status);
        var audit = Assert.Single(await db.AuditLogs.AsNoTracking().Where(x => x.EntityId == claimId || x.EntityId == donationId).ToListAsync());
        Assert.Equal("ClaimCancelled", audit.Action); Assert.Equal(nameof(DonationClaim), audit.EntityType); Assert.Equal(claimId, audit.EntityId);
        Assert.Equal(actorUserId, audit.ActorUserId); Assert.Equal(Now, audit.CreatedAtUtc);
        Assert.Contains($"DonationId={donationId}", audit.Details); Assert.Contains($"DonationStatus=Claimed->{donationStatus}", audit.Details);
        Assert.False(await db.QrVerificationTokens.AnyAsync(x => x.DonationClaimId == claimId));
        Assert.False(await db.HandoverRecords.AnyAsync(x => x.DonationClaimId == claimId));
    }
    private async Task<bool> InMarketplaceAsync(Guid donationId)
    {
        await using var db = fixture.CreateContext();
        var marketplace = new FoodDonationRepository(db, new FixedClock(Now));
        for (var page = 1; ; page++)
        {
            var items = await marketplace.GetAvailableAsync(page, 100);
            if (items.Any(x => x.Id == donationId)) return true;
            if (items.Count < 100) return false;
        }
    }

    // ---- Success
    [Fact]
    public async Task Cancel_own_booked_unassigned_claim_returns_donation_to_available()
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync(); var (claimId, donationId) = await SeedClaimForCancelAsync(organizationId!.Value);
        Assert.Equal(new CancelClaimResult(CancelClaimOutcome.Cancelled, DonationStatus.Available), await CancelAsync(Principal(userId), claimId));
        await AssertCancelledAsync(claimId, donationId, userId, DonationStatus.Available);
    }
    [Fact]
    public async Task Cancel_returned_available_donation_is_visible_in_marketplace()
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync(); var (claimId, donationId) = await SeedClaimForCancelAsync(organizationId!.Value);
        Assert.False(await InMarketplaceAsync(donationId));
        Assert.Equal(CancelClaimOutcome.Cancelled, (await CancelAsync(Principal(userId), claimId)).Outcome);
        Assert.True(await InMarketplaceAsync(donationId));
    }
    [Fact]
    public async Task Cancel_returned_donation_can_be_claimed_again_by_another_beneficiary()
    {
        var (userA, orgA) = await SeedBeneficiaryAsync(); var (userB, orgB) = await SeedBeneficiaryAsync();
        var (claimId, donationId) = await SeedClaimForCancelAsync(orgA!.Value);
        Assert.Equal(CancelClaimOutcome.Cancelled, (await CancelAsync(Principal(userA), claimId)).Outcome);

        var reclaim = await CreateAsync(Principal(userB), donationId);
        Assert.Equal(CreateClaimOutcome.Created, reclaim.Outcome);
        await AssertPersistedAsync(donationId, DonationStatus.Claimed, claims: 2, activeClaims: 1, claimCreatedAudits: 1);
        await using var db = fixture.CreateContext();
        Assert.Equal(ClaimStatus.Cancelled, (await db.DonationClaims.AsNoTracking().SingleAsync(x => x.Id == claimId)).Status);
        var active = (await new ClaimRepository(db).GetActiveForDonationAsync(donationId))!;
        Assert.Equal(reclaim.ClaimId, active.Id); Assert.Equal(orgB, active.BeneficiaryOrganizationId); Assert.Equal(ClaimStatus.Booked, active.Status);
    }

    // ---- Donation returns to Draft
    private async Task AssertCancelReturnsDraftAsync(DateTimeOffset? expiresAtUtc = null,
        OrganizationStatus donorStatus = OrganizationStatus.Active, OrganizationType donorType = OrganizationType.Donor)
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync();
        var (claimId, donationId) = await SeedClaimForCancelAsync(organizationId!.Value, expiresAtUtc: expiresAtUtc, donorStatus: donorStatus, donorType: donorType);
        Assert.Equal(new CancelClaimResult(CancelClaimOutcome.Cancelled, DonationStatus.Draft), await CancelAsync(Principal(userId), claimId));
        await AssertCancelledAsync(claimId, donationId, userId, DonationStatus.Draft);
        Assert.False(await InMarketplaceAsync(donationId));
    }
    [Fact]
    public Task Cancel_of_expired_donation_returns_it_to_draft() => AssertCancelReturnsDraftAsync(expiresAtUtc: Now.AddMinutes(-1));
    [Fact]
    public Task Cancel_of_donation_expiring_exactly_now_returns_it_to_draft() => AssertCancelReturnsDraftAsync(expiresAtUtc: Now);
    [Theory]
    [InlineData(OrganizationStatus.Pending)]
    [InlineData(OrganizationStatus.Rejected)]
    [InlineData(OrganizationStatus.Suspended)]
    public Task Cancel_with_inactive_donor_returns_donation_to_draft(OrganizationStatus donorStatus) => AssertCancelReturnsDraftAsync(donorStatus: donorStatus);
    [Fact]
    public Task Cancel_with_non_donor_type_organization_returns_donation_to_draft() => AssertCancelReturnsDraftAsync(donorType: OrganizationType.Beneficiary);

    // ---- Caller and organization rejections
    [Fact]
    public async Task Cancel_rejects_unauthenticated_user_without_writes()
    {
        var (_, organizationId) = await SeedBeneficiaryAsync(); var (claimId, _) = await SeedClaimForCancelAsync(organizationId!.Value);
        await AssertRejectedAsync(new ClaimsPrincipal(new ClaimsIdentity()), claimId, CancelClaimOutcome.Unauthenticated);
    }
    [Theory]
    [InlineData(AppRoles.Donor)]
    [InlineData(AppRoles.Courier)]
    [InlineData(AppRoles.Admin)]
    public async Task Cancel_rejects_wrong_role_without_writes(string role)
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync(); var (claimId, _) = await SeedClaimForCancelAsync(organizationId!.Value);
        await AssertRejectedAsync(Principal(userId, role), claimId, CancelClaimOutcome.Forbidden);
    }
    [Theory]
    [InlineData(OrganizationStatus.Pending)]
    [InlineData(OrganizationStatus.Rejected)]
    [InlineData(OrganizationStatus.Suspended)]
    public async Task Cancel_rejects_beneficiary_organization_not_active_without_writes(OrganizationStatus status)
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync(status); var (claimId, _) = await SeedClaimForCancelAsync(organizationId!.Value);
        await AssertRejectedAsync(Principal(userId), claimId, CancelClaimOutcome.OrganizationNotActive);
    }
    [Fact]
    public async Task Cancel_rejected_for_suspended_beneficiary_leaves_history_readable()
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync(OrganizationStatus.Suspended); var (claimId, _) = await SeedClaimForCancelAsync(organizationId!.Value);
        await AssertRejectedAsync(Principal(userId), claimId, CancelClaimOutcome.OrganizationNotActive);
        var history = await GetMyClaimsAsync(Principal(userId));
        Assert.Equal(GetMyClaimsOutcome.Success, history.Outcome);
        var summary = Assert.Single(history.Claims);
        Assert.Equal(claimId, summary.ClaimId); Assert.Equal(ClaimStatus.Booked, summary.ClaimStatus);
    }
    [Fact]
    public async Task Cancel_rejects_beneficiary_without_linked_organization()
    {
        var (userId, _) = await SeedUserAsync(null); var (_, organizationId) = await SeedBeneficiaryAsync();
        var (claimId, _) = await SeedClaimForCancelAsync(organizationId!.Value);
        await AssertRejectedAsync(Principal(userId), claimId, CancelClaimOutcome.OrganizationNotActive);
    }
    [Fact]
    public async Task Cancel_rejects_beneficiary_role_linked_to_donor_organization()
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync(type: OrganizationType.Donor);
        var (claimId, _) = await SeedClaimForCancelAsync(organizationId!.Value);
        await AssertRejectedAsync(Principal(userId), claimId, CancelClaimOutcome.OrganizationNotActive);
    }

    // ---- Ownership: a foreign claim is indistinguishable from a missing one
    [Theory]
    [InlineData(OrganizationStatus.Active)]
    [InlineData(OrganizationStatus.Suspended)] // ownership is checked before organization status, so status reveals nothing either
    public async Task Cancel_of_another_organizations_claim_is_claim_not_found(OrganizationStatus callerStatus)
    {
        var (userId, _) = await SeedBeneficiaryAsync(callerStatus); var (_, otherOrganizationId) = await SeedBeneficiaryAsync();
        var (foreignClaimId, _) = await SeedClaimForCancelAsync(otherOrganizationId!.Value);
        await AssertRejectedAsync(Principal(userId), foreignClaimId, CancelClaimOutcome.ClaimNotFound);
        Assert.Equal(await CancelAsync(Principal(userId), Guid.NewGuid()), await CancelAsync(Principal(userId), foreignClaimId));
        await using var db = fixture.CreateContext();
        Assert.False(await db.AuditLogs.AnyAsync(x => x.ActorUserId == userId));
    }
    [Fact]
    public async Task Cancel_of_nonexistent_claim_is_claim_not_found()
    {
        var (userId, _) = await SeedBeneficiaryAsync();
        Assert.Equal(new CancelClaimResult(CancelClaimOutcome.ClaimNotFound), await CancelAsync(Principal(userId), Guid.NewGuid()));
        await using var db = fixture.CreateContext();
        Assert.False(await db.AuditLogs.AnyAsync(x => x.ActorUserId == userId));
    }

    // ---- Claim / donation eligibility
    [Fact]
    public async Task Cancel_rejects_booked_claim_with_assigned_courier()
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync(); var (courierId, _) = await SeedUserAsync(null);
        var (claimId, _) = await SeedClaimForCancelAsync(organizationId!.Value, courierUserId: courierId);
        await AssertRejectedAsync(Principal(userId), claimId, CancelClaimOutcome.NotCancellable);
    }
    [Theory]
    [InlineData(ClaimStatus.PickupPending)]
    [InlineData(ClaimStatus.PickedUp)]
    [InlineData(ClaimStatus.InTransit)]
    [InlineData(ClaimStatus.Delivered)]
    [InlineData(ClaimStatus.Closed)]
    [InlineData(ClaimStatus.Cancelled)]
    [InlineData(ClaimStatus.Failed)]
    public async Task Cancel_rejects_claim_that_is_not_booked(ClaimStatus status)
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync(); var (claimId, _) = await SeedClaimForCancelAsync(organizationId!.Value, status);
        await AssertRejectedAsync(Principal(userId), claimId, CancelClaimOutcome.NotCancellable);
    }
    [Theory]
    [InlineData(DonationStatus.Draft)]
    [InlineData(DonationStatus.Available)]
    [InlineData(DonationStatus.PickupPending)]
    [InlineData(DonationStatus.Expired)]
    [InlineData(DonationStatus.Cancelled)]
    public async Task Cancel_rejects_booked_claim_whose_donation_is_not_claimed(DonationStatus donationStatus)
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync();
        var (claimId, _) = await SeedClaimForCancelAsync(organizationId!.Value, donationStatus: donationStatus);
        await AssertRejectedAsync(Principal(userId), claimId, CancelClaimOutcome.NotCancellable);
    }
    [Fact]
    public async Task Cancel_twice_is_not_cancellable_and_writes_one_audit()
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync(); var (claimId, donationId) = await SeedClaimForCancelAsync(organizationId!.Value);
        Assert.Equal(CancelClaimOutcome.Cancelled, (await CancelAsync(Principal(userId), claimId)).Outcome);
        await AssertRejectedAsync(Principal(userId), claimId, CancelClaimOutcome.NotCancellable);
        await AssertCancelledAsync(claimId, donationId, userId, DonationStatus.Available);
    }

    // ---- Concurrency: each request stages from the same RowVersion, then saves in a fixed order. No timing involved.
    // GatedUnitOfWork completes its staged source on save, so a second SaveChanges (a retry) would throw.
    private sealed class SaveGate
    {
        public TaskCompletionSource Staged { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IUnitOfWork Wrap(ApplicationDbContext db) => new GatedUnitOfWork(new UnitOfWork(db), Staged, Release.Task);
    }
    private sealed class SingleCourierDirectory(Guid courierUserId) : ICourierDirectory
    {
        public Task<bool> IsCourierAsync(Guid id) => Task.FromResult(id == courierUserId);
        public Task<IReadOnlyList<CourierOption>> ListAsync() => Task.FromResult<IReadOnlyList<CourierOption>>([new(courierUserId, "Courier")]);
    }
    private static CourierService AssignService(ApplicationDbContext db, ClaimsPrincipal principal, Guid courierUserId, IUnitOfWork unitOfWork)
    {
        var clock = new FixedClock(Now);
        var currentUser = new CurrentUserService(new FixedHttpContextAccessor(new DefaultHttpContext { User = principal }), db);
        return new CourierService(currentUser, new CourierRepository(db), new SingleCourierDirectory(courierUserId),
            new Repository<Organization>(db), unitOfWork, new AuditService(db, currentUser, clock), clock);
    }
    private static void AssertStagedFrom(ApplicationDbContext db, PersistedState seeded)
    {
        var claim = db.ChangeTracker.Entries<DonationClaim>().Single();
        var donation = db.ChangeTracker.Entries<FoodDonation>().Single();
        Assert.Equal(EntityState.Modified, claim.State); Assert.Equal(EntityState.Modified, donation.State);
        Assert.Equal(EntityState.Added, db.ChangeTracker.Entries<AuditLog>().Single().State);
        Assert.Equal(seeded.ClaimRowVersion, Convert.ToHexString(claim.Property(x => x.RowVersion).OriginalValue));
        Assert.Equal(seeded.DonationRowVersion, Convert.ToHexString(donation.Property(x => x.RowVersion).OriginalValue));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancel_vs_assign_from_the_same_row_version_commits_exactly_one(bool cancelSavesFirst)
    {
        var (beneficiaryUserId, organizationId) = await SeedBeneficiaryAsync();
        var (adminUserId, _) = await SeedUserAsync(null); var (courierUserId, _) = await SeedUserAsync(null);
        var (claimId, donationId) = await SeedClaimForCancelAsync(organizationId!.Value);
        var seeded = await ReadStateAsync(claimId);

        var cancelGate = new SaveGate(); var assignGate = new SaveGate();
        await using var cancelDb = fixture.CreateContext(); await using var assignDb = fixture.CreateContext();
        var cancel = Service(cancelDb, Principal(beneficiaryUserId), cancelGate.Wrap(cancelDb)).CancelAsync(claimId, CancellationToken.None);
        var assign = AssignService(assignDb, Principal(adminUserId, AppRoles.Admin), courierUserId, assignGate.Wrap(assignDb))
            .AssignAsync(claimId, courierUserId, CancellationToken.None);

        // Both passed validation and staged claim + donation + audit from the seeded RowVersions; neither has saved.
        await Task.WhenAll(cancelGate.Staged.Task, assignGate.Staged.Task).WaitAsync(TimeSpan.FromSeconds(30));
        AssertStagedFrom(cancelDb, seeded); AssertStagedFrom(assignDb, seeded);

        var (first, firstRequest, second) = cancelSavesFirst ? (cancelGate, (Task)cancel, assignGate) : (assignGate, assign, cancelGate);
        first.Release.SetResult();
        await firstRequest.WaitAsync(TimeSpan.FromSeconds(30));
        second.Release.SetResult();
        var cancelResult = await cancel.WaitAsync(TimeSpan.FromSeconds(30));
        var assignResult = await assign.WaitAsync(TimeSpan.FromSeconds(30));

        await using var verify = fixture.CreateContext();
        var claim = await verify.DonationClaims.AsNoTracking().Include(x => x.FoodDonation).SingleAsync(x => x.Id == claimId);
        var audits = await verify.AuditLogs.AsNoTracking().Where(x => x.EntityId == claimId || x.EntityId == donationId).ToListAsync();
        var audit = Assert.Single(audits); // no orphan audit from the loser
        Assert.Equal(claimId, audit.EntityId);
        if (cancelSavesFirst)
        {
            Assert.Equal(new CancelClaimResult(CancelClaimOutcome.Cancelled, DonationStatus.Available), cancelResult);
            Assert.Equal(new CourierResult(false, "This task changed. Refresh and try again."), assignResult);
            Assert.Equal(ClaimStatus.Cancelled, claim.Status); Assert.Null(claim.AssignedCourierUserId);
            Assert.Equal(DonationStatus.Available, claim.FoodDonation.Status);
            Assert.Equal("ClaimCancelled", audit.Action);
        }
        else
        {
            Assert.Equal(new CancelClaimResult(CancelClaimOutcome.Conflict), cancelResult);
            Assert.True(assignResult.Succeeded);
            Assert.Equal(ClaimStatus.PickupPending, claim.Status); Assert.Equal(courierUserId, claim.AssignedCourierUserId);
            Assert.Equal(DonationStatus.PickupPending, claim.FoodDonation.Status);
            Assert.Equal("CourierAssigned", audit.Action);
        }
        Assert.False(await verify.QrVerificationTokens.AnyAsync(x => x.DonationClaimId == claimId));
        Assert.False(await verify.HandoverRecords.AnyAsync(x => x.DonationClaimId == claimId));
    }
    [Fact]
    public async Task Cancel_vs_cancel_from_the_same_row_version_commits_exactly_one()
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync(); var (claimId, donationId) = await SeedClaimForCancelAsync(organizationId!.Value);
        var seeded = await ReadStateAsync(claimId);

        var gateA = new SaveGate(); var gateB = new SaveGate();
        await using var dbA = fixture.CreateContext(); await using var dbB = fixture.CreateContext();
        var requestA = Service(dbA, Principal(userId), gateA.Wrap(dbA)).CancelAsync(claimId, CancellationToken.None);
        var requestB = Service(dbB, Principal(userId), gateB.Wrap(dbB)).CancelAsync(claimId, CancellationToken.None);
        await Task.WhenAll(gateA.Staged.Task, gateB.Staged.Task).WaitAsync(TimeSpan.FromSeconds(30));
        AssertStagedFrom(dbA, seeded); AssertStagedFrom(dbB, seeded);

        gateA.Release.SetResult();
        Assert.Equal(new CancelClaimResult(CancelClaimOutcome.Cancelled, DonationStatus.Available), await requestA.WaitAsync(TimeSpan.FromSeconds(30)));
        gateB.Release.SetResult();
        Assert.Equal(new CancelClaimResult(CancelClaimOutcome.Conflict), await requestB.WaitAsync(TimeSpan.FromSeconds(30)));
        await AssertCancelledAsync(claimId, donationId, userId, DonationStatus.Available);
    }
}
