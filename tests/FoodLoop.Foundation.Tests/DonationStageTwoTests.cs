using FoodLoop.Application.Donations;
using FoodLoop.Application.Identity;
using FoodLoop.Application.Interfaces.Identity;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Auditing;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Foundation.Tests;

public sealed class DonationStageTwoTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static readonly DateTimeOffset Now = new(2030, 5, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Clock = new FixedTimeProvider(Now);

    [Fact]
    public async Task Active_owner_can_edit_draft_and_update_and_audit_are_saved_together()
    {
        var setup = await CreateDraftAsync();
        await using var db = fixture.CreateContext();
        var service = BuildService(db, setup.OwnerId);
        var edit = await service.GetForEditAsync(setup.DonationId);

        Assert.Equal(GetDonationForEditOutcome.Success, edit.Outcome);
        var result = await service.UpdateAsync(setup.DonationId, Request(
            edit.Donation!.RowVersion,
            setup.CategoryId,
            title: "  Updated meals  ",
            preparedAt: Now.AddHours(-3),
            expiresAt: Now.AddHours(-2)));

        Assert.True(result.Succeeded);
        await using var verify = fixture.CreateContext();
        var saved = await verify.FoodDonations.AsNoTracking().SingleAsync(x => x.Id == setup.DonationId);
        Assert.Equal("Updated meals", saved.Title);
        Assert.Equal(Now.AddHours(-2), saved.ExpiresAtUtc);
        Assert.Equal(DonationStatus.Draft, saved.Status);
        Assert.Equal(1, await verify.AuditLogs.CountAsync(x => x.EntityId == setup.DonationId && x.Action == "DonationUpdated"));
    }

    [Fact]
    public async Task Another_donor_and_a_published_donation_cannot_be_edited()
    {
        var setup = await CreateDraftAsync();
        Guid otherDonorId;
        await using (var arrange = fixture.CreateContext())
        {
            var other = Organization(OrganizationStatus.Active);
            arrange.Add(other);
            await arrange.SaveChangesAsync();
            otherDonorId = other.Id;
        }

        await using (var otherDb = fixture.CreateContext())
        {
            var donation = await otherDb.FoodDonations.AsNoTracking().SingleAsync(x => x.Id == setup.DonationId);
            var result = await BuildService(otherDb, otherDonorId).UpdateAsync(
                setup.DonationId,
                Request(Convert.ToBase64String(donation.RowVersion), setup.CategoryId));
            Assert.False(result.Succeeded);
            Assert.Contains("another organization", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        await using (var publish = fixture.CreateContext())
        {
            var donation = await publish.FoodDonations.SingleAsync(x => x.Id == setup.DonationId);
            donation.Status = DonationStatus.Available;
            await publish.SaveChangesAsync();
        }

        await using (var ownerDb = fixture.CreateContext())
        {
            var service = BuildService(ownerDb, setup.OwnerId);
            var edit = await service.GetForEditAsync(setup.DonationId);
            Assert.Equal(GetDonationForEditOutcome.NotDraft, edit.Outcome);

            var current = await ownerDb.FoodDonations.AsNoTracking().SingleAsync(x => x.Id == setup.DonationId);
            var result = await service.UpdateAsync(
                setup.DonationId,
                Request(Convert.ToBase64String(current.RowVersion), setup.CategoryId));
            Assert.False(result.Succeeded);
            Assert.Contains("draft", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        await using var verify = fixture.CreateContext();
        Assert.False(await verify.AuditLogs.AnyAsync(x => x.EntityId == setup.DonationId));
    }

    [Fact]
    public async Task Edit_opened_before_publish_is_rejected_as_stale_without_an_audit()
    {
        var setup = await CreateDraftAsync();
        string openedRowVersion;
        await using (var opened = fixture.CreateContext())
        {
            var edit = await BuildService(opened, setup.OwnerId).GetForEditAsync(setup.DonationId);
            openedRowVersion = edit.Donation!.RowVersion;
        }

        await using (var publish = fixture.CreateContext())
        {
            var donation = await publish.FoodDonations.SingleAsync(x => x.Id == setup.DonationId);
            donation.Status = DonationStatus.Available;
            await publish.SaveChangesAsync();
        }

        await using (var stale = fixture.CreateContext())
        {
            var result = await BuildService(stale, setup.OwnerId).UpdateAsync(
                setup.DonationId,
                Request(openedRowVersion, setup.CategoryId));
            Assert.False(result.Succeeded);
            Assert.Contains("Refresh", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        await using var verify = fixture.CreateContext();
        Assert.Equal(DonationStatus.Available, (await verify.FoodDonations.FindAsync(setup.DonationId))!.Status);
        Assert.False(await verify.AuditLogs.AnyAsync(x => x.EntityId == setup.DonationId));
    }

    [Fact]
    public async Task Marketplace_filters_before_stable_twenty_item_paging_and_hides_ineligible_rows()
    {
        await using var db = fixture.CreateContext();
        var active = Organization(OrganizationStatus.Active);
        var suspended = Organization(OrganizationStatus.Suspended);
        var selected = new FoodCategory { Name = "Selected " + Guid.NewGuid().ToString("N") };
        var other = new FoodCategory { Name = "Other " + Guid.NewGuid().ToString("N") };
        db.AddRange(active, suspended, selected, other);
        await db.SaveChangesAsync();

        for (var index = 0; index < 25; index++)
            db.Add(Donation(active.Id, selected.Id, $"Rice {index:D2}", Now.AddHours(index + 1)));
        db.Add(Donation(active.Id, selected.Id, "Bread", Now.AddDays(2)));
        db.Add(Donation(active.Id, other.Id, "Rice wrong category", Now.AddDays(2)));
        db.Add(Donation(active.Id, selected.Id, "Rice expired", Now.AddMinutes(-1)));
        db.Add(Donation(suspended.Id, selected.Id, "Rice suspended", Now.AddDays(2)));
        await db.SaveChangesAsync();

        var repository = new FoodDonationRepository(db, Clock);
        var first = await repository.GetAvailableAsync(" Rice ", selected.Id, 1, 20);
        var second = await repository.GetAvailableAsync("Rice", selected.Id, 2, 20);

        Assert.Equal(20, first.Items.Count);
        Assert.True(first.HasNext);
        Assert.Equal(5, second.Items.Count);
        Assert.False(second.HasNext);
        var all = first.Items.Concat(second.Items).ToList();
        Assert.Equal(25, all.Count);
        Assert.All(all, x =>
        {
            Assert.Equal(selected.Id, x.FoodCategoryId);
            Assert.Equal(active.Id, x.DonorOrganizationId);
            Assert.Contains("Rice", x.Title);
            Assert.True(x.ExpiresAtUtc > Now);
        });
        Assert.Equal(all.OrderBy(x => x.ExpiresAtUtc).ThenBy(x => x.Id).Select(x => x.Id), all.Select(x => x.Id));
    }

    private async Task<(Guid OwnerId, Guid CategoryId, Guid DonationId)> CreateDraftAsync()
    {
        await using var db = fixture.CreateContext();
        var owner = Organization(OrganizationStatus.Active);
        var category = new FoodCategory { Name = "Edit " + Guid.NewGuid().ToString("N") };
        db.AddRange(owner, category);
        await db.SaveChangesAsync();
        var donation = Donation(owner.Id, category.Id, "Draft meals", Now.AddHours(2));
        donation.Status = DonationStatus.Draft;
        db.Add(donation);
        await db.SaveChangesAsync();
        return (owner.Id, category.Id, donation.Id);
    }

    private static DonationService BuildService(ApplicationDbContext db, Guid organizationId)
    {
        var currentUser = new TestUser(organizationId);
        return new DonationService(
            currentUser,
            new FoodDonationRepository(db, Clock),
            new FoodCategoryRepository(db),
            new Repository<Organization>(db),
            new UnitOfWork(db),
            new AuditService(db, currentUser, Clock),
            Clock);
    }

    private static UpdateDonationRequest Request(
        string rowVersion,
        Guid categoryId,
        string title = "Updated",
        DateTimeOffset? preparedAt = null,
        DateTimeOffset? expiresAt = null)
        => new(
            categoryId,
            title,
            "Description",
            12,
            QuantityUnit.Meals,
            preparedAt ?? Now,
            expiresAt ?? Now.AddHours(2),
            "Keep covered",
            "Pickup address",
            rowVersion);

    private static Organization Organization(OrganizationStatus status) => new()
    {
        Name = "Donor " + Guid.NewGuid().ToString("N"),
        LicenseNumber = Guid.NewGuid().ToString("N"),
        Type = OrganizationType.Donor,
        Status = status,
        Address = "Address"
    };

    private static FoodDonation Donation(Guid donorId, Guid categoryId, string title, DateTimeOffset expiresAt) => new()
    {
        DonorOrganizationId = donorId,
        FoodCategoryId = categoryId,
        Title = title,
        Description = "Description",
        Quantity = 5,
        Unit = QuantityUnit.Meals,
        PreparedAtUtc = Now.AddHours(-1),
        ExpiresAtUtc = expiresAt,
        StorageInstructions = "Keep covered",
        PickupAddress = "Address",
        Status = DonationStatus.Available,
        CreatedAtUtc = Now
    };

    private sealed class TestUser(Guid organizationId) : ICurrentUserService
    {
        public Guid? UserId => null;
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => role == AppRoles.Donor;
        public Task<Guid?> GetOrganizationIdAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<Guid?>(organizationId);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
