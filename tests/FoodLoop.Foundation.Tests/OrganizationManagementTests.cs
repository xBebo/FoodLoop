using FoodLoop.Application.Identity;
using FoodLoop.Application.Interfaces.Identity;
using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Application.Organizations;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Auditing;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Foundation.Tests;

public sealed class OrganizationManagementTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private sealed record Actor(Guid Id, string Role) : ICurrentUserService
    {
        public Guid? UserId => Id;
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => role == Role;
        public Task<Guid?> GetOrganizationIdAsync(CancellationToken ct = default) => Task.FromResult<Guid?>(null);
    }

    private OrganizationManagementService Service(
        ApplicationDbContext db,
        Actor actor,
        IUnitOfWork? unitOfWork = null) => new(
        actor,
        new Repository<Organization>(db),
        new OrganizationAdminReadRepository(db),
        new AuditService(db, actor, TimeProvider.System),
        unitOfWork ?? new UnitOfWork(db));

    private static Organization Organization(string suffix, OrganizationStatus status) => new()
    {
        Name = $"Organization {suffix}",
        LicenseNumber = $"LIC-{suffix}",
        Type = OrganizationType.Beneficiary,
        Status = status,
        Address = "Test address"
    };

    private static ApplicationUser AdminUser(Guid id) => new()
    {
        Id = id,
        UserName = $"admin-{id:N}",
        DisplayName = "Organization Admin"
    };

    [Fact]
    public async Task Suspend_and_reactivate_change_status_and_record_one_audit_each()
    {
        await using var db = fixture.CreateContext();
        var admin = new Actor(Guid.NewGuid(), AppRoles.Admin);
        var organization = Organization(Guid.NewGuid().ToString("N"), OrganizationStatus.Active);
        db.Users.Add(AdminUser(admin.Id));
        db.Organizations.Add(organization);
        await db.SaveChangesAsync();

        var service = Service(db, admin);
        var suspended = await service.SuspendAsync(organization.Id);
        Assert.True(suspended.Succeeded, suspended.Error);
        Assert.Equal(OrganizationStatus.Suspended, organization.Status);
        Assert.Equal(1, await db.AuditLogs.CountAsync(x => x.EntityId == organization.Id && x.Action == "OrganizationSuspended"));
        Assert.Equal(admin.Id, (await db.AuditLogs.SingleAsync(x => x.EntityId == organization.Id && x.Action == "OrganizationSuspended")).ActorUserId);

        var reactivated = await service.ReactivateAsync(organization.Id);
        Assert.True(reactivated.Succeeded, reactivated.Error);
        Assert.Equal(OrganizationStatus.Active, organization.Status);
        Assert.Equal(1, await db.AuditLogs.CountAsync(x => x.EntityId == organization.Id && x.Action == "OrganizationReactivated"));
    }

    [Fact]
    public async Task Non_admin_and_invalid_state_are_rejected()
    {
        await using var db = fixture.CreateContext();
        var organization = Organization(Guid.NewGuid().ToString("N"), OrganizationStatus.Pending);
        db.Organizations.Add(organization);
        await db.SaveChangesAsync();

        var nonAdmin = new Actor(Guid.NewGuid(), AppRoles.Donor);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(db, nonAdmin).SuspendAsync(organization.Id));

        var admin = new Actor(Guid.NewGuid(), AppRoles.Admin);
        db.Users.Add(AdminUser(admin.Id));
        await db.SaveChangesAsync();
        var result = await Service(db, admin).SuspendAsync(organization.Id);
        Assert.Equal(OrganizationStatusChangeOutcome.InvalidState, result.Outcome);
        Assert.Equal(OrganizationStatus.Pending, organization.Status);
        Assert.False(await db.AuditLogs.AnyAsync(x => x.EntityId == organization.Id));
    }

    [Fact]
    public async Task Status_filter_is_applied_before_server_side_pagination()
    {
        await using var db = fixture.CreateContext();
        var tag = Guid.NewGuid().ToString("N");
        var baseline = await db.Organizations.CountAsync(x => x.Status == OrganizationStatus.Suspended);
        var added = Enumerable.Range(0, 21)
            .Select(i => Organization($"{tag}-{i:D2}", OrganizationStatus.Suspended))
            .ToList();
        db.Organizations.AddRange(added);
        db.Organizations.Add(Organization($"{tag}-active", OrganizationStatus.Active));
        await db.SaveChangesAsync();

        var repository = new OrganizationAdminReadRepository(db);
        var first = await repository.GetPageAsync(OrganizationStatus.Suspended, 1, 20);
        Assert.Equal(baseline + 21, first.TotalCount);
        Assert.All(first.Items, x => Assert.Equal(OrganizationStatus.Suspended, x.Status));
        Assert.True(first.Items.Count <= 20);

        var all = new List<OrganizationAdminItem>();
        for (var page = 1; page <= first.TotalPages; page++)
            all.AddRange((await repository.GetPageAsync(OrganizationStatus.Suspended, page, 20)).Items);

        Assert.All(added, organization => Assert.Contains(all, x => x.Id == organization.Id));
        Assert.Equal(all.OrderBy(x => x.Name).ThenBy(x => x.Id).Select(x => x.Id), all.Select(x => x.Id));
    }

    [Fact]
    public async Task Concurrent_update_returns_controlled_conflict_without_audit()
    {
        Guid organizationId;
        var admin = new Actor(Guid.NewGuid(), AppRoles.Admin);
        await using (var setup = fixture.CreateContext())
        {
            var organization = Organization(Guid.NewGuid().ToString("N"), OrganizationStatus.Active);
            setup.Users.Add(AdminUser(admin.Id));
            setup.Organizations.Add(organization);
            await setup.SaveChangesAsync();
            organizationId = organization.Id;
        }

        await using var db = fixture.CreateContext();
        var conflictUnit = new ConcurrentSave(db, () => fixture.CreateContext(), organizationId);
        var result = await Service(db, admin, conflictUnit).SuspendAsync(organizationId);
        Assert.Equal(OrganizationStatusChangeOutcome.Conflict, result.Outcome);

        await using var check = fixture.CreateContext();
        Assert.Equal(OrganizationStatus.Active, (await check.Organizations.SingleAsync(x => x.Id == organizationId)).Status);
        Assert.False(await check.AuditLogs.AnyAsync(x => x.EntityId == organizationId && x.Action == "OrganizationSuspended"));
    }

    private sealed class ConcurrentSave(
        ApplicationDbContext tracked,
        Func<ApplicationDbContext> otherContext,
        Guid organizationId) : IUnitOfWork
    {
        private bool changed;

        public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            if (!changed)
            {
                await using var other = otherContext();
                var organization = await other.Organizations.SingleAsync(x => x.Id == organizationId, ct);
                organization.Address = $"Concurrent-{Guid.NewGuid():N}";
                await other.SaveChangesAsync(ct);
                changed = true;
            }
            return await new UnitOfWork(tracked).SaveChangesAsync(ct);
        }

        public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
            new UnitOfWork(tracked).BeginTransactionAsync(ct);
    }
}
