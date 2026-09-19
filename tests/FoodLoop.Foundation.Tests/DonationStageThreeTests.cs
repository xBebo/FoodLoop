using System.Text;
using FoodLoop.Application;
using FoodLoop.Application.Claims;
using FoodLoop.Application.Donations;
using FoodLoop.Application.Identity;
using FoodLoop.Application.Interfaces.Identity;
using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Auditing;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Persistence.Repositories;
using FoodLoop.Web.Controllers;
using FoodLoop.Web.Models.Donations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FoodLoop.Foundation.Tests;

public sealed class DonationStageThreeTests(DatabaseFixture fixture, WebApplicationFactory<Program> web)
    : IClassFixture<DatabaseFixture>, IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly DateTimeOffset Now = new(2030, 5, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Clock = new FixedTimeProvider(Now);

    [Fact]
    public void AddApplication_registers_expiry_use_case_once_as_scoped()
    {
        var descriptor = Assert.Single(new ServiceCollection().AddApplication(),
            x => x.ServiceType == typeof(IDonationExpiryService));
        Assert.Equal(typeof(DonationExpiryService), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public async Task Expiry_changes_only_available_due_donations_and_repeat_run_is_idempotent()
    {
        var expected = new Dictionary<Guid, DonationStatus>();
        await using (var arrange = fixture.CreateContext())
        {
            var donor = Donor(); var category = Category();
            arrange.AddRange(donor, category); await arrange.SaveChangesAsync();
            void Add(DonationStatus status, DateTimeOffset expiresAtUtc, DonationStatus expectedStatus)
            {
                var donation = Donation(donor.Id, category.Id, status, expiresAtUtc);
                arrange.Add(donation); expected.Add(donation.Id, expectedStatus);
            }
            Add(DonationStatus.Available, Now.AddSeconds(1), DonationStatus.Available);
            Add(DonationStatus.Available, Now, DonationStatus.Expired);
            Add(DonationStatus.Available, Now.AddMinutes(-1), DonationStatus.Expired);
            Add(DonationStatus.Draft, Now.AddMinutes(-1), DonationStatus.Draft);
            Add(DonationStatus.Claimed, Now.AddMinutes(-1), DonationStatus.Claimed);
            Add(DonationStatus.PickupPending, Now.AddMinutes(-1), DonationStatus.PickupPending);
            Add(DonationStatus.InTransit, Now.AddMinutes(-1), DonationStatus.InTransit);
            Add(DonationStatus.Closed, Now.AddMinutes(-1), DonationStatus.Closed);
            Add(DonationStatus.Expired, Now.AddMinutes(-1), DonationStatus.Expired);
            await arrange.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext())
        {
            var result = await ExpiryService(db).ExpireDueAsync();
            Assert.True(result.Succeeded); Assert.Equal(2, result.ExpiredCount);
        }
        await using (var db = fixture.CreateContext())
        {
            var result = await ExpiryService(db).ExpireDueAsync();
            Assert.True(result.Succeeded); Assert.Equal(0, result.ExpiredCount);
        }

        await using var verify = fixture.CreateContext();
        var actual = await verify.FoodDonations.AsNoTracking().Where(x => expected.Keys.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Status);
        Assert.Equal(expected, actual);
        var audits = await verify.AuditLogs.AsNoTracking()
            .Where(x => expected.Keys.Contains(x.EntityId) && x.Action == "DonationExpired").ToListAsync();
        Assert.Equal(2, audits.Count);
        Assert.All(audits, x =>
        {
            Assert.Equal(nameof(FoodDonation), x.EntityType);
            Assert.Equal("DonationStatus=Available->Expired", x.Details);
            Assert.Null(x.ActorUserId);
        });
    }

    [Fact]
    public async Task Claim_and_expiry_race_has_one_winner_and_no_orphan_state_or_audit()
    {
        Guid donationId; Guid beneficiaryId;
        await using (var arrange = fixture.CreateContext())
        {
            var donor = Donor();
            var beneficiary = new Organization
            {
                Name = "Beneficiary", LicenseNumber = Guid.NewGuid().ToString("N"),
                Type = OrganizationType.Beneficiary, Status = OrganizationStatus.Active, Address = "Beneficiary address"
            };
            var category = Category();
            arrange.AddRange(donor, beneficiary, category); await arrange.SaveChangesAsync();
            var donation = Donation(donor.Id, category.Id, DonationStatus.Available, Now);
            arrange.Add(donation); await arrange.SaveChangesAsync();
            donationId = donation.Id; beneficiaryId = beneficiary.Id;
        }

        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expiryStaged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var claimStaged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var expiryDb = fixture.CreateContext(); await using var claimDb = fixture.CreateContext();
        var expiryTask = ExpiryService(expiryDb,
            new GatedUnitOfWork(new UnitOfWork(expiryDb), expiryStaged, release.Task)).ExpireDueAsync();
        var user = new TestUser(beneficiaryId, AppRoles.Beneficiary);
        var claimClock = new FixedTimeProvider(Now.AddSeconds(-1));
        var claimTask = new ClaimService(user, new FoodDonationRepository(claimDb, claimClock),
            new Repository<Organization>(claimDb), new ClaimRepository(claimDb), new ClaimDetailsReadRepository(claimDb),
            new AuditService(claimDb, user, claimClock),
            new GatedUnitOfWork(new UnitOfWork(claimDb), claimStaged, release.Task), claimClock)
            .CreateAsync(donationId, CancellationToken.None);

        await Task.WhenAll(expiryStaged.Task, claimStaged.Task).WaitAsync(TimeSpan.FromSeconds(30));
        release.SetResult();
        var expiry = await expiryTask.WaitAsync(TimeSpan.FromSeconds(30));
        var claim = await claimTask.WaitAsync(TimeSpan.FromSeconds(30));
        var expiryWon = expiry.Succeeded && expiry.ExpiredCount == 1;
        var claimWon = claim.Outcome == CreateClaimOutcome.Created;
        Assert.True(expiryWon ^ claimWon);

        await using var verify = fixture.CreateContext();
        var persisted = await verify.FoodDonations.AsNoTracking().SingleAsync(x => x.Id == donationId);
        var expiryAudits = await verify.AuditLogs.Where(x => x.EntityId == donationId && x.Action == "DonationExpired").ToListAsync();
        var claims = await verify.DonationClaims.Where(x => x.FoodDonationId == donationId).ToListAsync();
        if (expiryWon)
        {
            Assert.Equal(CreateClaimOutcome.Conflict, claim.Outcome);
            Assert.Equal(DonationStatus.Expired, persisted.Status);
            Assert.Empty(claims); Assert.Single(expiryAudits);
        }
        else
        {
            Assert.False(expiry.Succeeded); Assert.Equal(0, expiry.ExpiredCount);
            Assert.Equal(DonationStatus.Claimed, persisted.Status);
            Assert.Single(claims); Assert.Empty(expiryAudits);
        }
        Assert.Equal(1, await verify.AuditLogs.CountAsync(x =>
            (x.EntityId == donationId && x.Action == "DonationExpired") ||
            (x.Action == "ClaimCreated" && x.Details!.Contains(donationId.ToString()))));
    }

    [Fact]
    public async Task My_donations_filter_is_owned_and_stably_ordered_and_details_do_not_disclose_foreign_rows()
    {
        Guid ownerId; Guid categoryId; Guid newestClaimed; Guid olderClaimed; Guid foreignDonation;
        await using (var arrange = fixture.CreateContext())
        {
            var owner = Donor("Owner"); var other = Donor("Other"); var category = Category();
            arrange.AddRange(owner, other, category); await arrange.SaveChangesAsync();
            ownerId = owner.Id; categoryId = category.Id;
            var draft = WithCreated(Donation(ownerId, categoryId, DonationStatus.Draft, Now.AddHours(2)), Now.AddMinutes(-4));
            var older = WithCreated(Donation(ownerId, categoryId, DonationStatus.Claimed, Now.AddHours(2)), Now.AddMinutes(-3));
            var newest = WithCreated(Donation(ownerId, categoryId, DonationStatus.Claimed, Now.AddHours(2)), Now.AddMinutes(-1));
            var foreign = WithCreated(Donation(other.Id, categoryId, DonationStatus.Claimed, Now.AddHours(2)), Now);
            arrange.AddRange(draft, older, newest, foreign); await arrange.SaveChangesAsync();
            newestClaimed = newest.Id; olderClaimed = older.Id; foreignDonation = foreign.Id;
        }

        await using var db = fixture.CreateContext();
        var service = DonationService(db, new TestUser(ownerId, AppRoles.Donor));
        var claimed = await service.GetMineAsync(DonationStatus.Claimed);
        Assert.Equal([newestClaimed, olderClaimed], claimed.Select(x => x.Id));
        Assert.All(claimed, x => Assert.Equal(DonationStatus.Claimed, x.Status));
        var details = await service.GetDetailsAsync(newestClaimed);
        Assert.NotNull(details); Assert.Equal("Donation", details.Title);
        Assert.StartsWith("Stage 3 category ", details.Category);
        Assert.Equal("Pickup address", details.PickupAddress);
        Assert.Equal("Keep covered", details.StorageInstructions);
        Assert.Null(await service.GetDetailsAsync(foreignDonation));
        Assert.DoesNotContain(foreignDonation, (await service.GetMineAsync()).Select(x => x.Id));
        var controller = new DonationsController(service);
        var ownView = Assert.IsType<ViewResult>(await controller.Details(newestClaimed, CancellationToken.None));
        Assert.Equal(newestClaimed, Assert.IsType<DonationDetailsItem>(ownView.Model).Id);
        Assert.IsType<NotFoundResult>(await controller.Details(foreignDonation, CancellationToken.None));
    }

    [Fact]
    public async Task Donation_views_render_required_details_filters_utc_and_mobile_card_hooks()
    {
        var statuses = new[] { DonationStatus.Draft, DonationStatus.Available, DonationStatus.Claimed, DonationStatus.Closed, DonationStatus.Expired };
        var items = statuses.Select((status, index) => new DonationListItem(Guid.NewGuid(), $"Donation {index}", "Meals", 2,
            QuantityUnit.Meals, Now.AddHours(-1), Now.AddHours(2), status, "Pickup address")).ToList();
        var mineHtml = await RenderDonationViewAsync(nameof(DonationsController.Mine),
            new MyDonationsViewModel(items, DonationStatus.Expired));
        Assert.Contains("All times are shown in UTC", mineHtml);
        Assert.Contains("<option value=\"Expired\" selected=\"selected\">Expired</option>", mineHtml);
        Assert.Equal(items.Count, Count(mineHtml, "href=\"/Donations/Details/"));
        Assert.Equal(items.Count, Count(mineHtml, "data-label=\"Title\""));
        Assert.Equal(items.Count, Count(mineHtml, "data-label=\"Actions\""));
        foreach (var status in statuses) Assert.Contains($">{status}</span>", mineHtml);
        Assert.Contains("2030-05-10T14:00:00Z", mineHtml);
        Assert.Contains("10 May 2030, 14:00 UTC", mineHtml);

        var detailsHtml = await RenderDonationViewAsync(nameof(DonationsController.Details), new DonationDetailsItem(
            Guid.NewGuid(), "Rice trays", "Vegetable rice", "Cooked meals", 12.5m, QuantityUnit.Kilograms,
            Now.AddHours(-3), Now.AddHours(8), "1 Pickup Street", "Keep chilled", DonationStatus.Available));
        foreach (var text in new[] { "Rice trays", "Cooked meals", "12.5 Kilograms", "1 Pickup Street", "Keep chilled", "Vegetable rice", "Available" })
            Assert.Contains(text, detailsHtml);
        Assert.Contains("All times are shown in UTC", detailsHtml);
        Assert.Contains("2030-05-10T09:00:00Z", detailsHtml);
        Assert.Contains("10 May 2030, 09:00 UTC", detailsHtml);
        Assert.Contains("2030-05-10T20:00:00Z", detailsHtml);
        Assert.Contains("10 May 2030, 20:00 UTC", detailsHtml);
    }

    private async Task<string> RenderDonationViewAsync<TModel>(string action, TModel model)
    {
        using var scope = web.Services.CreateScope();
        var routeValues = new RouteValueDictionary { ["controller"] = "Donations", ["action"] = action };
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        httpContext.Request.RouteValues = routeValues;
        httpContext.SetEndpoint(new Endpoint(null, null, $"Donations/{action}"));
        using var body = new MemoryStream(); httpContext.Response.Body = body;
        var view = new ViewResult
        {
            ViewName = action,
            ViewData = new ViewDataDictionary<TModel>(scope.ServiceProvider.GetRequiredService<IModelMetadataProvider>(),
                new ModelStateDictionary()) { Model = model },
            TempData = new TempDataDictionary(httpContext, scope.ServiceProvider.GetRequiredService<ITempDataProvider>())
        };
        await view.ExecuteResultAsync(new ActionContext(httpContext, new RouteData(routeValues),
            new ControllerActionDescriptor { RouteValues = { ["controller"] = "Donations", ["action"] = action } }));
        return Encoding.UTF8.GetString(body.ToArray());
    }

    private static int Count(string source, string value)
        => (source.Length - source.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;

    private static DonationExpiryService ExpiryService(ApplicationDbContext db, IUnitOfWork? unitOfWork = null)
    {
        var user = new TestUser(null, string.Empty);
        return new DonationExpiryService(new FoodDonationRepository(db, Clock), unitOfWork ?? new UnitOfWork(db),
            new AuditService(db, user, Clock), Clock);
    }

    private static DonationService DonationService(ApplicationDbContext db, ICurrentUserService user)
        => new(user, new FoodDonationRepository(db, Clock), new FoodCategoryRepository(db),
            new Repository<Organization>(db), new UnitOfWork(db), new AuditService(db, user, Clock), Clock);

    private static Organization Donor(string name = "Donor") => new()
    {
        Name = name, LicenseNumber = Guid.NewGuid().ToString("N"), Type = OrganizationType.Donor,
        Status = OrganizationStatus.Active, Address = "Donor address"
    };
    private static FoodCategory Category() => new() { Name = "Stage 3 category " + Guid.NewGuid().ToString("N") };
    private static FoodDonation Donation(Guid donorId, Guid categoryId, DonationStatus status, DateTimeOffset expiresAtUtc) => new()
    {
        DonorOrganizationId = donorId, FoodCategoryId = categoryId, Title = "Donation", Description = "Description",
        Quantity = 5, Unit = QuantityUnit.Meals, PreparedAtUtc = Now.AddHours(-3), ExpiresAtUtc = expiresAtUtc,
        StorageInstructions = "Keep covered", PickupAddress = "Pickup address", Status = status, CreatedAtUtc = Now
    };
    private static FoodDonation WithCreated(FoodDonation donation, DateTimeOffset createdAtUtc)
    { donation.CreatedAtUtc = createdAtUtc; return donation; }

    private sealed class TestUser(Guid? organizationId, string role, Guid? userId = null) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public bool IsAuthenticated => organizationId is not null;
        public bool IsInRole(string requestedRole) => requestedRole == role;
        public Task<Guid?> GetOrganizationIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(organizationId);
    }
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class GatedUnitOfWork(IUnitOfWork inner, TaskCompletionSource staged, Task release) : IUnitOfWork
    {
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            staged.TrySetResult(); await release.WaitAsync(cancellationToken);
            return await inner.SaveChangesAsync(cancellationToken);
        }
        public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => inner.BeginTransactionAsync(cancellationToken);
    }
}
