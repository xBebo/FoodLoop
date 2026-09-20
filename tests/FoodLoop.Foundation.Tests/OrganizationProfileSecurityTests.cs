using FoodLoop.Application.Organizations;
using FoodLoop.Application.Interfaces.Identity;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Persistence.Repositories;
using FoodLoop.Web.Controllers;
using FoodLoop.Web.Models.Organizations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Foundation.Tests;

public sealed class OrganizationProfileSecurityTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task Stale_submitted_rowversion_is_rejected_and_does_not_persist_audit()
    {
        Guid orgId;
        Guid userId;
        byte[] staleVersion;

        await using (var setup = fixture.CreateContext())
        {
            var org = NewOrganization();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = Guid.NewGuid().ToString("N"),
                Organization = org
            };
            setup.AddRange(org, user);
            await setup.SaveChangesAsync();
            orgId = org.Id;
            userId = user.Id;
            staleVersion = org.RowVersion.ToArray();
        }

        await using (var concurrent = fixture.CreateContext())
        {
            var org = await concurrent.Organizations.FindAsync(orgId);
            org!.Name = "Changed elsewhere";
            await concurrent.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext())
        {
            var currentUser = new TestCurrentUser(userId, orgId);
            var service = new OrganizationProfileService(
                new OrganizationProfileRepository(db),
                new Repository<AuditLog>(db),
                new UnitOfWork(db),
                currentUser,
                TimeProvider.System);

            var error = await service.UpdateProfileAsync(
                "Stale overwrite",
                "Stale address",
                staleVersion);

            Assert.NotNull(error);
        }

        await using var verify = fixture.CreateContext();
        Assert.Equal("Changed elsewhere", (await verify.Organizations.FindAsync(orgId))!.Name);
        Assert.False(await verify.AuditLogs.AnyAsync(x => x.EntityId == orgId && x.Action == "OrganizationUpdated"));
    }

    [Fact]
    public async Task Suspended_organization_is_read_only_and_direct_update_is_rejected()
    {
        await using var db = fixture.CreateContext();
        var org = NewOrganization();
        org.Status = OrganizationStatus.Suspended;
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = Guid.NewGuid().ToString("N"),
            Organization = org
        };
        db.AddRange(org, user);
        await db.SaveChangesAsync();

        var service = new OrganizationProfileService(
            new OrganizationProfileRepository(db),
            new Repository<AuditLog>(db),
            new UnitOfWork(db),
            new TestCurrentUser(user.Id, org.Id),
            TimeProvider.System);

        var error = await service.UpdateProfileAsync("Changed", "Changed", org.RowVersion);

        Assert.NotNull(error);
        Assert.NotEqual("Changed", org.Name);
        Assert.False(await db.AuditLogs.AnyAsync(x => x.EntityId == org.Id && x.Action == "OrganizationUpdated"));
    }

    [Theory]
    [InlineData("/Courier/MyTasks", true)]
    [InlineData("/MyOrganization/Index", true)]
    [InlineData("https://malicious-site.com", false)]
    [InlineData("//malicious-site.com", false)]
    [InlineData(@"/\malicious-site.com", false)]
    public async Task MyOrganization_controller_uses_framework_local_url_validation(
        string returnUrl,
        bool shouldRedirectToReturnUrl)
    {
        await using var db = fixture.CreateContext();
        var org = NewOrganization();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = Guid.NewGuid().ToString("N"),
            Organization = org
        };
        db.AddRange(org, user);
        await db.SaveChangesAsync();

        var service = new OrganizationProfileService(
            new OrganizationProfileRepository(db),
            new Repository<AuditLog>(db),
            new UnitOfWork(db),
            new TestCurrentUser(user.Id, org.Id),
            TimeProvider.System);

        var controller = new MyOrganizationController(service);
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor());
        controller.ControllerContext = new ControllerContext(actionContext);
        controller.Url = new UrlHelper(actionContext);

        var model = new OrganizationProfileViewModel
        {
            Id = org.Id,
            Name = "Updated",
            Address = "Updated address",
            LicenseNumber = org.LicenseNumber,
            Type = org.Type,
            Status = org.Status,
            RowVersion = org.RowVersion
        };

        var result = await controller.Edit(model, returnUrl);

        if (shouldRedirectToReturnUrl)
        {
            var redirect = Assert.IsType<RedirectResult>(result);
            Assert.Equal(returnUrl, redirect.Url);
        }
        else
        {
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(MyOrganizationController.Index), redirect.ActionName);
        }
    }

    private static Organization NewOrganization() => new()
    {
        Name = "Profile test",
        LicenseNumber = Guid.NewGuid().ToString("N"),
        Type = OrganizationType.Donor,
        Status = OrganizationStatus.Active,
        Address = "Original address"
    };

    private sealed class TestCurrentUser(Guid userId, Guid organizationId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => true;
        public Task<Guid?> GetOrganizationIdAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<Guid?>(organizationId);
    }
}
