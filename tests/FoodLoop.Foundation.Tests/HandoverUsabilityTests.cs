using FoodLoop.Application.Courier;
using FoodLoop.Application.Identity;
using FoodLoop.Application.Interfaces.Auditing;
using FoodLoop.Application.Interfaces.Identity;
using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Foundation.Tests;

public sealed class HandoverUsabilityTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);

    private sealed class FrozenClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed record Actor(Guid Id, string Role, Guid? OrganizationId = null) : ICurrentUserService
    {
        public Guid? UserId => Id;
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => role == Role;
        public Task<Guid?> GetOrganizationIdAsync(CancellationToken ct = default) => Task.FromResult(OrganizationId);
    }

    private sealed class Directory(Guid courierId) : ICourierDirectory
    {
        public Task<IReadOnlyList<CourierOption>> ListAsync() =>
            Task.FromResult<IReadOnlyList<CourierOption>>([new(courierId, "Courier")]);
        public Task<bool> IsCourierAsync(Guid id) => Task.FromResult(id == courierId);
    }

    private sealed class NoAudit : IAuditService
    {
        public void Record(string action, string entityType, Guid entityId, string? details = null) { }
    }

    [Theory]
    [InlineData(5)]
    [InlineData(60)]
    public async Task Issued_code_returns_the_actual_persisted_expiry(int donationExpiresInMinutes)
    {
        await using var db = fixture.CreateContext();
        var donor = new Organization
        {
            Name = "Expiry Donor",
            LicenseNumber = Guid.NewGuid().ToString("N"),
            Type = OrganizationType.Donor,
            Status = OrganizationStatus.Active
        };
        var beneficiary = new Organization
        {
            Name = "Expiry Beneficiary",
            LicenseNumber = Guid.NewGuid().ToString("N"),
            Type = OrganizationType.Beneficiary,
            Status = OrganizationStatus.Active
        };
        var category = new FoodCategory { Name = $"Expiry-{Guid.NewGuid():N}" };
        var donation = new FoodDonation
        {
            DonorOrganization = donor,
            FoodCategory = category,
            Title = "Expiry test meal",
            Quantity = 1,
            Unit = QuantityUnit.Meals,
            PreparedAtUtc = Now.AddHours(-1),
            ExpiresAtUtc = Now.AddMinutes(donationExpiresInMinutes),
            Status = DonationStatus.Claimed,
            PickupAddress = "Test address"
        };
        var claim = new DonationClaim
        {
            FoodDonation = donation,
            BeneficiaryOrganization = beneficiary,
            Status = ClaimStatus.Booked
        };
        var courierId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser
        {
            Id = courierId,
            UserName = $"courier-{Guid.NewGuid():N}",
            DisplayName = "Expiry Courier"
        });
        db.DonationClaims.Add(claim);
        await db.SaveChangesAsync();

        var clock = new FrozenClock();
        CourierService Service(Actor actor) => new(
            actor,
            new CourierRepository(db),
            new Directory(courierId),
            new Repository<Organization>(db),
            new UnitOfWork(db),
            new NoAudit(),
            clock);

        var admin = new Actor(Guid.NewGuid(), AppRoles.Admin);
        var donorUser = new Actor(Guid.NewGuid(), AppRoles.Donor, donor.Id);
        Assert.True((await Service(admin).AssignAsync(claim.Id, courierId, default)).Succeeded);

        var result = await Service(donorUser).IssueAsync(claim.Id, HandoverType.Pickup, default);
        Assert.True(result.Succeeded, result.Error);
        Assert.NotNull(result.Token);
        Assert.Equal(64, result.Token!.Length);
        Assert.NotNull(result.ExpiresAtUtc);

        var expectedExpiry = Now.AddMinutes(Math.Min(15, donationExpiresInMinutes));
        Assert.Equal(expectedExpiry, result.ExpiresAtUtc);
        var stored = await db.QrVerificationTokens.SingleAsync(x => x.DonationClaimId == claim.Id && x.UsedAtUtc == null);
        Assert.Equal(expectedExpiry, stored.ExpiresAtUtc);
    }
}
