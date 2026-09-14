using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FoodLoop.Application.Admin;
using FoodLoop.Application.Interfaces.Identity;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace FoodLoop.Foundation.Tests;

public sealed class AdminTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
    private sealed class FrozenClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class WebHost(string connection) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection", connection);
            builder.ConfigureTestServices(services => {
                services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(new FrozenClock());
                // Only this test host accepts the test-role header. Production keeps Identity cookies.
                services.AddAuthentication(options => options.DefaultAuthenticateScheme = "Test")
                    .AddScheme<AuthenticationSchemeOptions, RoleHandler>("Test", _ => { });
            });
        }
    }
    private sealed class RoleHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].ToString();
            if (string.IsNullOrEmpty(role)) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role)], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
    private HttpClient Client(WebHost host, string? role = null)
    {
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        if (role is not null) client.DefaultRequestHeaders.Add("X-Test-Role", role);
        return client;
    }
    [Theory]
    [InlineData("/Admin")]
    [InlineData("/Admin/Audit")]
    public async Task Anonymous_requests_redirect_to_Janas_login_route(string path)
    {
        using var host = new WebHost(fixture.ConnectionString); using var client = Client(host);
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Auth/Login?ReturnUrl=", response.Headers.Location!.ToString());
    }
    [Theory]
    [InlineData("Donor", "/Admin")]
    [InlineData("Beneficiary", "/Admin")]
    [InlineData("Courier", "/Admin")]
    [InlineData("Donor", "/Admin/Audit")]
    [InlineData("Beneficiary", "/Admin/Audit")]
    [InlineData("Courier", "/Admin/Audit")]
    public async Task Non_admins_are_forbidden_on_both_endpoints(string role, string path)
    {
        using var host = new WebHost(fixture.ConnectionString); using var client = Client(host, role);
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Admin/AccessDenied", response.Headers.Location!.ToString());
        var denied = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Contains("Access denied", await denied.Content.ReadAsStringAsync());
    }
    [Theory]
    [InlineData("/Admin", "Operations overview")]
    [InlineData("/Admin/Audit", "Audit log")]
    [InlineData("/Admin/Audit?page=-100", "Audit log")]
    [InlineData("/Admin/Audit?page=2147483647", "Audit log")]
    public async Task Admin_can_render_pages_and_extreme_page_numbers(string path, string expected)
    {
        using var host = new WebHost(fixture.ConnectionString); using var client = Client(host, "Admin");
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expected, await response.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task Audit_pagination_is_stable_projects_actor_and_handles_empty_page()
    {
        await using var db = fixture.CreateContext(); var repo = new AdminReadRepository(db);
        var empty = await repo.GetAuditPageAsync(int.MaxValue, 20);
        Assert.Empty(empty.Items); Assert.Equal(1, empty.Page); Assert.Equal(1, empty.TotalPages);
        var actor = new ApplicationUser { Id = Guid.NewGuid(), DisplayName = "Baraa", UserName = "admin-test" };
        db.Users.Add(actor);
        var entries = Enumerable.Range(0, 23).Select(i => new AuditLog { Action = i == 0 ? "<script>alert(1)</script>" : "ClaimCreated",
            ActorUserId = i == 0 ? actor.Id : null, EntityType = "DonationClaim", EntityId = Guid.NewGuid(), CreatedAtUtc = Now }).ToList();
        db.AuditLogs.AddRange(entries); await db.SaveChangesAsync();
        var first = await repo.GetAuditPageAsync(1, 20); var second = await repo.GetAuditPageAsync(2, 20);
        Assert.Equal(23, first.TotalCount); Assert.Equal(20, first.Items.Count); Assert.Equal(3, second.Items.Count);
        Assert.Empty(first.Items.Select(x => x.Id).Intersect(second.Items.Select(x => x.Id)));
        Assert.Equal(first.Items.Select(x => x.Id), (await repo.GetAuditPageAsync(1, 20)).Items.Select(x => x.Id));
        Assert.Equal(2, (await repo.GetAuditPageAsync(int.MaxValue, 20)).Page);
        Assert.Contains(first.Items.Concat(second.Items), x => x.ActorName == "Baraa" && x.ActorUserId == actor.Id);
        Assert.Contains(first.Items.Concat(second.Items), x => x.ActorName == "System" && x.ActorUserId == null);
        using var host = new WebHost(fixture.ConnectionString); using var client = Client(host, "Admin");
        var html = await client.GetStringAsync("/Admin/Audit"); html += await client.GetStringAsync("/Admin/Audit?page=2");
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }
    [Fact]
    public async Task Dashboard_counts_only_eligible_listings_and_documented_closed_operations()
    {
        await using var db = fixture.CreateContext();
        Organization Org(OrganizationStatus status, OrganizationType type = OrganizationType.Donor) => new()
        { Name = "Test", LicenseNumber = Guid.NewGuid().ToString("N"), Status = status, Type = type, Address = "Test" };
        FoodDonation Donation(OrganizationStatus donorStatus = OrganizationStatus.Active) => new()
        { DonorOrganization = Org(donorStatus), FoodCategory = new FoodCategory { Name = Guid.NewGuid().ToString("N") },
            Title = "Test", Quantity = 2, Unit = QuantityUnit.Meals, PreparedAtUtc = Now.AddDays(-1), ExpiresAtUtc = Now.AddHours(1),
            PickupAddress = "Test", Status = DonationStatus.Available };
        var good = Donation(); var atExpiry = Donation(); atExpiry.ExpiresAtUtc = Now;
        var expired = Donation(); expired.ExpiresAtUtc = Now.AddMinutes(-1);
        var suspended = Donation(OrganizationStatus.Suspended); var pendingDonor = Donation(OrganizationStatus.Pending);
        var rejected = Donation(OrganizationStatus.Rejected); var draft = Donation(); draft.Status = DonationStatus.Draft;
        var closed = Donation(); closed.Status = DonationStatus.Closed;
        var noEvidence = Donation(); noEvidence.Status = DonationStatus.Closed;
        var notClosed = Donation(); notClosed.Status = DonationStatus.Delivered;
        var courier = new ApplicationUser { Id = Guid.NewGuid(), UserName = Guid.NewGuid().ToString("N") };
        DonationClaim ClaimFor(FoodDonation donation, ClaimStatus status) => new()
        { FoodDonation = donation, BeneficiaryOrganization = Org(OrganizationStatus.Active, OrganizationType.Beneficiary), Status = status };
        var success = ClaimFor(closed, ClaimStatus.Closed); var unfinished = ClaimFor(notClosed, ClaimStatus.Delivered);
        db.AddRange(good, atExpiry, expired, suspended, pendingDonor, rejected, draft, courier, success, unfinished, ClaimFor(noEvidence, ClaimStatus.Closed));
        db.AddRange(new HandoverRecord { DonationClaim = success, CourierUserId = courier.Id, Type = HandoverType.Delivery, CompletedAtUtc = Now },
            new HandoverRecord { DonationClaim = success, CourierUserId = courier.Id, Type = HandoverType.Pickup, CompletedAtUtc = Now.AddMinutes(-5) },
            new HandoverRecord { DonationClaim = unfinished, CourierUserId = courier.Id, Type = HandoverType.Delivery, CompletedAtUtc = Now });
        await db.SaveChangesAsync();
        var result = await new AdminReadRepository(db).GetDashboardAsync(Now);
        Assert.Equal(new DashboardSummary(1, 1, 1), result);
        using var host = new WebHost(fixture.ConnectionString); using var client = Client(host, "Admin");
        var html = await client.GetStringAsync("/Admin");
        Assert.Contains("data-testid=\"available\">1", html);
    }
}
