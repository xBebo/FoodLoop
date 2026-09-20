using System.Net;
using System.Text.RegularExpressions;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FoodLoop.Foundation.Tests;

// A single fresh migrated SQL database, real Identity cookies, MVC forms and antiforgery.
public sealed class StageThreeReleaseJourneyTests
{
    [Fact]
    public async Task Fresh_database_HTTP_journey_preserves_lifecycle_security_metrics_and_audit()
    {
        var fixture = new DatabaseFixture();
        await fixture.InitializeAsync();
        try
        {
            using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
                .UseEnvironment("Development")
                .UseSetting("ConnectionStrings:DefaultConnection", fixture.ConnectionString)
                .UseSetting("DonationExpiryScheduler:IntervalSeconds", "1"));
            var accounts = new Dictionary<string, ApplicationUser>();
            var password = "Journey!9-" + Guid.NewGuid().ToString("N");
            Guid categoryId;
            await using (var scope = host.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                foreach (var role in new[] { "Admin", "Donor", "Beneficiary", "Courier" })
                    Assert.True((await roles.CreateAsync(new(role))).Succeeded);
                foreach (var name in new[] { "Admin", "Donor", "Beneficiary", "Courier", "ForeignDonor", "ForeignBeneficiary", "ForeignCourier" })
                {
                    var role = name.Replace("Foreign", "");
                    var user = new ApplicationUser { UserName = name + "@journey.test", Email = name + "@journey.test", DisplayName = name };
                    if (role is "Donor" or "Beneficiary")
                        user.Organization = new Organization { Name = name, LicenseNumber = Guid.NewGuid().ToString("N"),
                            Type = Enum.Parse<OrganizationType>(role), Status = OrganizationStatus.Active };
                    Assert.True((await users.CreateAsync(user, password)).Succeeded);
                    Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
                    accounts.Add(name, user);
                }
                var category = new FoodCategory { Name = "Journey meals" };
                db.Add(category);
                await db.SaveChangesAsync();
                categoryId = category.Id;
            }

            async Task<HttpClient> Login(string name)
            {
                var client = host.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new("https://localhost") });
                await Post(client, "/Auth/Login", "/Auth/Login", new() { ["email"] = accounts[name].Email!, ["password"] = password });
                return client;
            }
            using var donor = await Login("Donor");
            using var beneficiary = await Login("Beneficiary");
            using var admin = await Login("Admin");
            using var courier = await Login("Courier");
            using var foreignDonor = await Login("ForeignDonor");
            using var foreignBeneficiary = await Login("ForeignBeneficiary");
            using var foreignCourier = await Login("ForeignCourier");
            await using var check = fixture.CreateContext();

            async Task<Guid> Publish(string title)
            {
                await Post(donor, "/Donations/Create", "/Donations/Create", new() {
                    ["Title"] = title, ["FoodCategoryId"] = categoryId.ToString(), ["Quantity"] = "5", ["Unit"] = "Meals",
                    ["PreparedAt"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"),
                    ["ExpiresAt"] = DateTimeOffset.UtcNow.AddHours(1).ToString("O"), ["PickupAddress"] = "Journey address" });
                var id = await check.FoodDonations.Where(x => x.Title == title).Select(x => x.Id).SingleAsync();
                await Post(donor, "/Donations/Mine", "/Donations/Publish", new() { ["id"] = id.ToString() });
                Assert.Equal(DonationStatus.Available, await check.FoodDonations.Where(x => x.Id == id).Select(x => x.Status).SingleAsync());
                return id;
            }
            async Task<Guid> Claim(Guid id)
            {
                await Post(beneficiary, "/Donations/Available", "/Claims/Create", new() { ["donationId"] = id.ToString() });
                return await check.DonationClaims.Where(x => x.FoodDonationId == id && x.Status == ClaimStatus.Booked).Select(x => x.Id).SingleAsync();
            }
            var donationId = await Publish("Journey closure");
            Assert.Contains("Journey closure", await beneficiary.GetStringAsync("/Donations/Available"));
            Assert.Equal(HttpStatusCode.NotFound, (await foreignDonor.GetAsync($"/Donations/Details/{donationId}")).StatusCode);
            var cancelled = await Claim(donationId);
            Assert.Equal(HttpStatusCode.NotFound, (await foreignBeneficiary.GetAsync($"/Claims/Details?claimId={cancelled}")).StatusCode);
            var foreignCancel = await Post(foreignBeneficiary, "/Claims/Mine", "/Claims/Cancel", new() {
                ["claimId"] = cancelled.ToString() }, HttpStatusCode.NotFound);
            Assert.Equal(ClaimStatus.Booked, await check.DonationClaims.Where(x => x.Id == cancelled).Select(x => x.Status).SingleAsync());
            await Post(beneficiary, "/Claims/Mine", "/Claims/Cancel", new() { ["claimId"] = cancelled.ToString() });
            Assert.Equal(ClaimStatus.Cancelled, await check.DonationClaims.Where(x => x.Id == cancelled).Select(x => x.Status).SingleAsync());
            var claimId = await Claim(donationId);
            await Post(admin, "/Courier/AssignCourier", "/Courier/AssignCourier", new() {
                ["claimId"] = claimId.ToString(), ["courierUserId"] = accounts["Courier"].Id.ToString() });
            Assert.Equal(HttpStatusCode.NotFound, (await foreignCourier.GetAsync($"/Courier/Details/{claimId}")).StatusCode);
            var forbidden = await donor.GetAsync("/Admin");
            Assert.Contains("/Admin/AccessDenied", forbidden.Headers.Location!.ToString());

            async Task<string> Issue(HttpClient owner, string type)
            {
                var response = await Post(owner, "/Courier/Codes", "/Courier/IssueCode", new() {
                    ["claimId"] = claimId.ToString(), ["handoverType"] = type }, HttpStatusCode.OK);
                Assert.True(response.Headers.CacheControl!.NoStore);
                var html = await response.Content.ReadAsStringAsync();
                var raw = Regex.Match(html, ">([A-F0-9]{64})</pre>").Groups[1].Value;
                Assert.Equal(64, raw.Length);
                return raw;
            }
            async Task Verify(string raw, string type, HttpStatusCode expected)
            {
                var response = await Post(courier, $"/Courier/VerifyHandover?claimId={claimId}", "/Courier/VerifyHandover", new() {
                    ["claimId"] = claimId.ToString(), ["handoverType"] = type, ["handoverToken"] = raw }, expected);
                Assert.DoesNotContain(raw, await response.Content.ReadAsStringAsync());
            }
            var pickup = await Issue(donor, "Pickup");
            var foreignIssue = await Post(foreignDonor, "/Courier/Codes", "/Courier/IssueCode", new() {
                ["claimId"] = claimId.ToString(), ["handoverType"] = "Pickup" });
            Assert.Contains("/Admin/AccessDenied", foreignIssue.Headers.Location!.ToString());
            var foreignVerify = await Post(foreignCourier, "/Courier/MyTasks", "/Courier/VerifyHandover", new() {
                ["claimId"] = claimId.ToString(), ["handoverType"] = "Pickup", ["handoverToken"] = pickup });
            Assert.Contains("/Admin/AccessDenied", foreignVerify.Headers.Location!.ToString());
            var auditsBefore = await check.AuditLogs.CountAsync();
            await Verify(pickup, "Delivery", HttpStatusCode.OK);
            Assert.Equal(auditsBefore, await check.AuditLogs.CountAsync());
            Assert.Empty(await check.HandoverRecords.ToListAsync());
            await Verify(pickup, "Pickup", HttpStatusCode.Redirect);
            await Verify(pickup, "Pickup", HttpStatusCode.OK);
            var delivery = await Issue(beneficiary, "Delivery");
            await Verify(delivery, "Delivery", HttpStatusCode.Redirect);
            var evidencePage = await courier.GetStringAsync($"/Courier/Details/{claimId}");
            Assert.Contains("Closed", evidencePage);
            Assert.Contains("Pickup Evidence Verified", evidencePage);
            Assert.Contains("Delivery Evidence Verified", evidencePage);
            Assert.DoesNotContain(pickup, evidencePage);
            Assert.DoesNotContain(delivery, evidencePage);
            Assert.Equal(2, await check.HandoverRecords.CountAsync());
            auditsBefore = await check.AuditLogs.CountAsync();
            // The closed page has no verification form; use the existing antiforgery-bearing tasks page.
            await Post(courier, "/Courier/MyTasks", "/Courier/VerifyHandover", new() {
                ["claimId"] = claimId.ToString(), ["handoverType"] = "Delivery", ["handoverToken"] = delivery }, HttpStatusCode.OK);
            Assert.Equal(auditsBefore, await check.AuditLogs.CountAsync());
            Assert.Equal(2, await check.QrVerificationTokens.CountAsync(x => x.UsedAtUtc != null));
            Assert.All(await check.QrVerificationTokens.Select(x => x.TokenHash).ToListAsync(), hash => {
                Assert.NotEqual(pickup, hash); Assert.NotEqual(delivery, hash);
            });

            var availableId = await Publish("Journey available");
            var expiredId = await Publish("Journey expiry");
            // Controlled time setup; all lifecycle transitions still go through HTTP/the hosted scheduler.
            await check.FoodDonations.Where(x => x.Id == expiredId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAtUtc, DateTimeOffset.UtcNow.AddSeconds(-1)));
            await Post(beneficiary, "/Donations/Available", "/Claims/Create", new() { ["donationId"] = expiredId.ToString() });
            Assert.False(await check.DonationClaims.AnyAsync(x => x.FoodDonationId == expiredId));
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                while (!await check.FoodDonations.AnyAsync(x => x.Id == expiredId && x.Status == DonationStatus.Expired, timeout.Token))
                    await Task.Delay(100, timeout.Token);
            }
            await Task.Delay(1200);
            var expiryAudit = Assert.Single(await check.AuditLogs.Where(x => x.EntityId == expiredId && x.Action == "DonationExpired").ToListAsync());
            Assert.Null(expiryAudit.ActorUserId);
            var summary = await new AdminReadRepository(check).GetDashboardAsync(DateTimeOffset.UtcNow);
            Assert.Equal(new FoodLoop.Application.Admin.DashboardSummary(0, 1, 1, 1, 1), summary);
            Assert.Contains("Journey available", await beneficiary.GetStringAsync("/Donations/Available"));

            await check.Organizations.Where(x => x.Id == accounts["Beneficiary"].OrganizationId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, OrganizationStatus.Suspended));
            Assert.Contains("Journey closure", await beneficiary.GetStringAsync("/Claims/Mine"));
            Assert.Contains("Read-Only Access", await beneficiary.GetStringAsync("/MyOrganization"));
            Assert.Contains("Journey closure", await beneficiary.GetStringAsync($"/Claims/Details?claimId={claimId}"));
            var countBefore = await check.DonationClaims.CountAsync();
            var rejected = await Post(beneficiary, "/Claims/Mine", "/Claims/Create", new() { ["donationId"] = availableId.ToString() });
            Assert.Contains("/Admin/AccessDenied", rejected.Headers.Location!.ToString());
            Assert.Equal(countBefore, await check.DonationClaims.CountAsync());
            var org = await check.Organizations.AsNoTracking().SingleAsync(x => x.Id == accounts["Beneficiary"].OrganizationId);
            await Post(beneficiary, "/MyOrganization", "/MyOrganization/Edit", new() {
                ["Name"] = "Forbidden change", ["Address"] = "Forbidden address", ["RowVersion"] = Convert.ToBase64String(org.RowVersion) });
            Assert.Equal(org.Name, await check.Organizations.Where(x => x.Id == org.Id).Select(x => x.Name).SingleAsync());

            await check.Organizations.Where(x => x.Id == accounts["Donor"].OrganizationId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, OrganizationStatus.Suspended));
            Assert.Equal(0, (await new AdminReadRepository(check).GetDashboardAsync(DateTimeOffset.UtcNow)).AvailableDonations);
            var auditHtml = await admin.GetStringAsync("/Admin/Audit");
            var auditData = string.Join("\n", await check.AuditLogs.Select(x => x.Details).ToListAsync());
            foreach (var raw in new[] { pickup, delivery })
            {
                Assert.DoesNotContain(raw, auditHtml);
                Assert.DoesNotContain(raw, auditData);
            }
            Assert.Equal(1, await check.AuditLogs.CountAsync(x => x.EntityId == claimId && x.Action == "PickupVerified"));
            Assert.Equal(1, await check.AuditLogs.CountAsync(x => x.EntityId == claimId && x.Action == "DeliveryVerified"));
        }
        finally { await fixture.DisposeAsync(); }
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, string formUrl, string target,
        Dictionary<string, string> fields, HttpStatusCode expected = HttpStatusCode.Redirect)
    {
        var html = await client.GetStringAsync(formUrl);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(token);
        var response = await client.PostAsync(target, new FormUrlEncodedContent(fields));
        Assert.Equal(expected, response.StatusCode);
        return response;
    }
}
