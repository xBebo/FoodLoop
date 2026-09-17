using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
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
    [InlineData("/Admin/Audit?actionName=ClaimCreated&actor=Test&from=2026-01-01T00%3A00&to=2026-01-02T00%3A00")]
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
    [InlineData("Donor", "/Admin/Audit?actionName=ClaimCreated")]
    [InlineData("Beneficiary", "/Admin/Audit?actionName=ClaimCreated&actor=Test&from=2026-01-01T00%3A00&to=2026-01-02T00%3A00")]
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
        var tag = Guid.NewGuid().ToString("N");
        var action = $"Pagination-{tag}";
        var filter = new AuditFilter(Action: action);
        var empty = await repo.GetAuditPageAsync(int.MaxValue, 20, filter);
        Assert.Empty(empty.Items); Assert.Equal(1, empty.Page); Assert.Equal(1, empty.TotalPages);
        var actorName = $"Baraa-{tag}";
        var actor = new ApplicationUser { Id = Guid.NewGuid(), DisplayName = actorName, UserName = $"admin-{tag}" };
        db.Users.Add(actor);
        var unsafeEntityType = $"<script>alert('{tag}')</script>";
        var entries = Enumerable.Range(0, 23).Select(i => new AuditLog { Action = action,
            ActorUserId = i == 0 ? actor.Id : null, EntityType = i == 0 ? unsafeEntityType : "DonationClaim",
            EntityId = Guid.NewGuid(), CreatedAtUtc = Now }).ToList();
        db.AuditLogs.AddRange(entries); await db.SaveChangesAsync();
        var first = await repo.GetAuditPageAsync(1, 20, filter); var second = await repo.GetAuditPageAsync(2, 20, filter);
        Assert.Equal(23, first.TotalCount); Assert.Equal(20, first.Items.Count); Assert.Equal(3, second.Items.Count);
        Assert.Empty(first.Items.Select(x => x.Id).Intersect(second.Items.Select(x => x.Id)));
        Assert.Equal(first.Items.Select(x => x.Id), (await repo.GetAuditPageAsync(1, 20, filter)).Items.Select(x => x.Id));
        Assert.Equal(2, (await repo.GetAuditPageAsync(int.MaxValue, 20, filter)).Page);
        Assert.Contains(first.Items.Concat(second.Items), x => x.ActorName == actorName && x.ActorUserId == actor.Id);
        Assert.Contains(first.Items.Concat(second.Items), x => x.ActorName == "System" && x.ActorUserId == null);
        using var host = new WebHost(fixture.ConnectionString); using var client = Client(host, "Admin");
        var request = $"/Admin/Audit?actionName={Uri.EscapeDataString(action)}";
        var html = await client.GetStringAsync(request); html += await client.GetStringAsync($"{request}&page=2");
        Assert.DoesNotContain(unsafeEntityType, html);
        Assert.Contains("&lt;script&gt;", html);
    }
    [Fact]
    public async Task Dashboard_links_to_admin_workflows()
    {
        using var host = new WebHost(fixture.ConnectionString); using var client = Client(host, "Admin");
        var html = await client.GetStringAsync("/Admin");
        Assert.Contains("href=\"/Organizations/PendingRequests\"", html);
        Assert.Contains("href=\"/Courier/AssignCourier\"", html);
        Assert.Contains("href=\"/Admin/Audit\"", html);
    }
    [Fact]
    public async Task Audit_filter_by_action_returns_only_matching_action_entries()
    {
        await using var db = fixture.CreateContext(); var repo = new AdminReadRepository(db);
        var tag = Guid.NewGuid().ToString("N");
        var wanted = $"WantedAction-{tag}"; var other = $"OtherAction-{tag}";
        db.AuditLogs.AddRange(
            new AuditLog { Action = wanted, EntityType = "Test", EntityId = Guid.NewGuid(), CreatedAtUtc = Now },
            new AuditLog { Action = wanted, EntityType = "Test", EntityId = Guid.NewGuid(), CreatedAtUtc = Now },
            new AuditLog { Action = other, EntityType = "Test", EntityId = Guid.NewGuid(), CreatedAtUtc = Now });
        await db.SaveChangesAsync();
        var result = await repo.GetAuditPageAsync(1, 20, new AuditFilter(Action: wanted));
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, x => Assert.Equal(wanted, x.Action));
    }
    [Fact]
    public async Task Audit_filter_by_actor_matches_computed_actor_name_and_excludes_others()
    {
        await using var db = fixture.CreateContext(); var repo = new AdminReadRepository(db);
        var tag = Guid.NewGuid().ToString("N")[..8];
        var actor = new ApplicationUser { Id = Guid.NewGuid(), DisplayName = $"ActorFilterTest-{tag}", UserName = "actorfilter-" + tag };
        var other = new ApplicationUser { Id = Guid.NewGuid(), DisplayName = "SomeoneElse", UserName = "someone-" + tag };
        db.Users.AddRange(actor, other);
        var action = $"ActorTag-{tag}";
        db.AuditLogs.AddRange(
            new AuditLog { Action = action, ActorUserId = actor.Id, EntityType = "Test", EntityId = Guid.NewGuid(), CreatedAtUtc = Now },
            new AuditLog { Action = action, ActorUserId = other.Id, EntityType = "Test", EntityId = Guid.NewGuid(), CreatedAtUtc = Now },
            new AuditLog { Action = action, ActorUserId = null, EntityType = "Test", EntityId = Guid.NewGuid(), CreatedAtUtc = Now });
        await db.SaveChangesAsync();
        var byName = await repo.GetAuditPageAsync(1, 20, new AuditFilter(Action: action, Actor: tag));
        Assert.Single(byName.Items); Assert.Equal(actor.Id, byName.Items[0].ActorUserId);
        var bySystem = await repo.GetAuditPageAsync(1, 20, new AuditFilter(Action: action, Actor: "System"));
        Assert.Single(bySystem.Items); Assert.Null(bySystem.Items[0].ActorUserId);
    }
    [Fact]
    public async Task Audit_date_range_filter_is_half_open_from_inclusive_to_exclusive()
    {
        await using var db = fixture.CreateContext(); var repo = new AdminReadRepository(db);
        var tag = Guid.NewGuid().ToString("N");
        var action = $"DateTag-{tag}";
        var before = Now.AddMinutes(-1); var at = Now; var after = Now.AddMinutes(1);
        db.AuditLogs.AddRange(
            new AuditLog { Action = action, EntityType = "Test", EntityId = Guid.NewGuid(), CreatedAtUtc = before },
            new AuditLog { Action = action, EntityType = "Test", EntityId = Guid.NewGuid(), CreatedAtUtc = at },
            new AuditLog { Action = action, EntityType = "Test", EntityId = Guid.NewGuid(), CreatedAtUtc = after });
        await db.SaveChangesAsync();
        var fromAt = await repo.GetAuditPageAsync(1, 20, new AuditFilter(Action: action, FromUtc: at));
        Assert.Equal(2, fromAt.TotalCount);
        Assert.DoesNotContain(fromAt.Items, x => x.TimestampUtc == before);
        var toAt = await repo.GetAuditPageAsync(1, 20, new AuditFilter(Action: action, ToUtc: at));
        Assert.Equal(1, toAt.TotalCount);
        Assert.DoesNotContain(toAt.Items, x => x.TimestampUtc == at || x.TimestampUtc == after);
        var range = await repo.GetAuditPageAsync(1, 20, new AuditFilter(Action: action, FromUtc: before, ToUtc: after));
        Assert.Equal(2, range.TotalCount);
        Assert.DoesNotContain(range.Items, x => x.TimestampUtc == after);
    }
    [Theory]
    [InlineData("2026-01-02T00%3A00", "2026-01-01T00%3A00")] // from > to
    [InlineData("2026-01-01T00%3A00", "2026-01-01T00%3A00")] // from == to (empty half-open range)
    public async Task Audit_invalid_date_range_shows_validation_error_without_a_server_error(string from, string to)
    {
        using var host = new WebHost(fixture.ConnectionString); using var client = Client(host, "Admin");
        var response = await client.GetAsync($"/Admin/Audit?from={from}&to={to}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("earlier than the end time", await response.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task Audit_filtered_results_over_a_page_paginate_and_total_count_reflects_the_filter()
    {
        await using var db = fixture.CreateContext(); var repo = new AdminReadRepository(db);
        var tag = Guid.NewGuid().ToString("N");
        var matchingAction = $"FilterTag-{tag}";
        var matching = Enumerable.Range(0, 25).Select(i => new AuditLog
        { Action = matchingAction, EntityType = "Test", EntityId = Guid.NewGuid(), CreatedAtUtc = Now.AddMinutes(-i) }).ToList();
        var nonMatching = Enumerable.Range(0, 10).Select(_ => new AuditLog
        { Action = $"Other-{tag}", EntityType = "Test", EntityId = Guid.NewGuid(), CreatedAtUtc = Now }).ToList();
        db.AuditLogs.AddRange(matching); db.AuditLogs.AddRange(nonMatching); await db.SaveChangesAsync();
        var filter = new AuditFilter(Action: matchingAction);
        var page1 = await repo.GetAuditPageAsync(1, 20, filter);
        var page2 = await repo.GetAuditPageAsync(2, 20, filter);
        Assert.Equal(25, page1.TotalCount); Assert.Equal(25, page2.TotalCount);
        Assert.Equal(20, page1.Items.Count); Assert.Equal(5, page2.Items.Count);
        Assert.All(page1.Items.Concat(page2.Items), x => Assert.Equal(matchingAction, x.Action));
        Assert.Empty(page1.Items.Select(x => x.Id).Intersect(page2.Items.Select(x => x.Id)));
        var combined = page1.Items.Concat(page2.Items).ToList();
        Assert.Equal(combined.OrderByDescending(x => x.TimestampUtc).ThenByDescending(x => x.Id).Select(x => x.Id), combined.Select(x => x.Id));
    }
    [Fact]
    public async Task Audit_query_string_filters_are_preserved_across_pagination_links()
    {
        await using var db = fixture.CreateContext();
        var tag = Guid.NewGuid().ToString("N");
        var action = $"QsTag-{tag}";
        var actorName = $"QsActor-{tag}";
        var actor = new ApplicationUser { Id = Guid.NewGuid(), DisplayName = actorName, UserName = "qsactor-" + tag };
        db.Users.Add(actor);
        var from = Now.AddDays(-1); var to = Now.AddDays(1);
        // 22 rows matching action + actor + date range so a Next link exists (page size 20).
        db.AuditLogs.AddRange(Enumerable.Range(0, 22).Select(i => new AuditLog
        { Action = action, ActorUserId = actor.Id, EntityType = "Test", EntityId = Guid.NewGuid(), CreatedAtUtc = Now.AddMinutes(-i) }));
        await db.SaveChangesAsync();
        using var host = new WebHost(fixture.ConnectionString); using var client = Client(host, "Admin");
        var requestUrl = "/Admin/Audit"
            + $"?actionName={Uri.EscapeDataString(action)}"
            + $"&actor={Uri.EscapeDataString(actorName)}"
            + $"&from={Uri.EscapeDataString(from.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss"))}"
            + $"&to={Uri.EscapeDataString(to.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss"))}";
        var html = await client.GetStringAsync(requestUrl);

        var nextMatch = Regex.Match(html, "<a[^>]*href=\"([^\"]+)\"[^>]*>Next</a>");
        Assert.True(nextMatch.Success, "Expected a Next pagination link with 22 filtered rows over a page size of 20.");
        var nextHref = WebUtility.HtmlDecode(nextMatch.Groups[1].Value);
        var query = ParseQuery(nextHref[(nextHref.IndexOf('?') + 1)..]);

        // Decode before comparing so we don't depend on one exact raw URL-encoding of ':' etc.
        Assert.Equal("2", query["page"]);
        Assert.Equal(action, query["actionName"]);
        Assert.Equal(actorName, query["actor"]);
        Assert.Equal(from, DateTimeOffset.Parse(query["from"], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal));
        Assert.Equal(to, DateTimeOffset.Parse(query["to"], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal));

        Assert.Contains($"id=\"actionName\" name=\"actionName\" value=\"{action}\"", html);
        Assert.Contains($"id=\"actor\" name=\"actor\" value=\"{actorName}\"", html);
        Assert.Contains($"id=\"from\" name=\"from\" value=\"{from.UtcDateTime:yyyy-MM-ddTHH:mm}\"", html);
        Assert.Contains($"id=\"to\" name=\"to\" value=\"{to.UtcDateTime:yyyy-MM-ddTHH:mm}\"", html);
        return;

        static Dictionary<string, string> ParseQuery(string query) =>
            query.Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Split('=', 2))
                .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p.Length > 1 ? p[1] : ""));
    }
    [Fact]
    public async Task Audit_filter_by_action_finds_ClaimCancelled_and_OrganizationSuspended_entries()
    {
        await using var db = fixture.CreateContext(); var repo = new AdminReadRepository(db);
        var claimEntity = Guid.NewGuid(); var orgEntity = Guid.NewGuid();
        db.AuditLogs.AddRange(
            new AuditLog { Action = "ClaimCancelled", EntityType = "DonationClaim", EntityId = claimEntity, CreatedAtUtc = Now },
            new AuditLog { Action = "OrganizationSuspended", EntityType = "Organization", EntityId = orgEntity, CreatedAtUtc = Now });
        await db.SaveChangesAsync();
        var cancelled = await repo.GetAuditPageAsync(1, 20, new AuditFilter(Action: "ClaimCancelled"));
        var suspended = await repo.GetAuditPageAsync(1, 20, new AuditFilter(Action: "OrganizationSuspended"));
        Assert.Contains(cancelled.Items, x => x.EntityId == claimEntity && x.EntityType == "DonationClaim");
        Assert.Contains(suspended.Items, x => x.EntityId == orgEntity && x.EntityType == "Organization");
        Assert.All(cancelled.Items, x => Assert.Equal("ClaimCancelled", x.Action));
        Assert.All(suspended.Items, x => Assert.Equal("OrganizationSuspended", x.Action));
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
