using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FoodLoop.Application.Identity;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FoodLoop.Foundation.Tests;

// Real host for the R6.3 marketplace/claim API. The expiry scheduler is off so "expired but still Available" rows stay put.
public sealed class MarketplaceApiHost : IAsyncLifetime
{
    public const string Password = "Test-Only!48269"; // Test-only password; never used for a real account.
    public DatabaseFixture Database { get; } = new();
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Database.InitializeAsync();
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseEnvironment("Development")
            .UseSetting("ConnectionStrings:DefaultConnection", Database.ConnectionString)
            .UseSetting("DonationExpiryScheduler:Enabled", "false"));
        await using var scope = Factory.Services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in AppRoles.All) Assert.True((await roles.CreateAsync(new(role))).Succeeded);
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await Database.DisposeAsync();
    }
}

public sealed class MarketplaceApiTests(MarketplaceApiHost host) : IClassFixture<MarketplaceApiHost>
{
    private const string Password = MarketplaceApiHost.Password;
    private const string Private = "(?i)organizationid|userid|assignedcourier|rowversion|tokenhash|audit|licensenumber|passwordhash|securitystamp";

    private HttpClient Client() => host.Factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

    private async Task<string> User(string role, OrganizationType? type = null, OrganizationStatus status = OrganizationStatus.Active)
    {
        await using var scope = host.Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@r63.local";
        var user = new ApplicationUser { UserName = email, Email = email, DisplayName = "R63 " + role };
        if (type is { } t)
            user.Organization = new Organization { Name = $"R63 {t} {Guid.NewGuid():N}", LicenseNumber = Guid.NewGuid().ToString("N"), Type = t, Status = status };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        return email;
    }

    private async Task<HttpClient> SignedIn(string role, OrganizationType? type = null, OrganizationStatus status = OrganizationStatus.Active)
    {
        var client = Client();
        var email = await User(role, type, status);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/api/auth/login", new { email, password = Password }, await Token(client))).StatusCode);
        return client;
    }

    private Task<HttpClient> Beneficiary() => SignedIn(AppRoles.Beneficiary, OrganizationType.Beneficiary);

    private static async Task<string> Token(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonObject>("/api/auth/antiforgery"))!["token"]!.GetValue<string>();

    private static async Task<HttpResponseMessage> Post(HttpClient client, string url, object? body, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = body is null ? null : JsonContent.Create(body) };
        if (token is not null) request.Headers.Add("X-XSRF-TOKEN", token);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> Claim(HttpClient client, Guid donationId) =>
        await Post(client, "/api/claims", new { donationId }, await Token(client));

    private static async Task<JsonNode> Json(HttpResponseMessage response) => JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    private static async Task Problem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = (await Json(response)).ToJsonString();
        Assert.Equal(code, JsonNode.Parse(body)!["code"]!.GetValue<string>());
        Assert.DoesNotMatch("(?i)exception|stack|sql|select |C:\\\\|\\.cs\\b|FoodLoop\\.|[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-", body.Replace(JsonNode.Parse(body)!["traceId"]?.GetValue<string>() ?? "\0", ""));
    }

    private sealed record Seeded(Guid CategoryId, string CategoryName, Guid DonorOrganizationId, string DonorName);

    private async Task<Seeded> Seed(OrganizationStatus donorStatus = OrganizationStatus.Active)
    {
        await using var db = host.Database.CreateContext();
        var category = new FoodCategory { Name = "R63 category " + Guid.NewGuid().ToString("N")[..8] };
        var donor = new Organization { Name = "R63 donor " + Guid.NewGuid().ToString("N")[..8], LicenseNumber = Guid.NewGuid().ToString("N"), Type = OrganizationType.Donor, Status = donorStatus };
        db.AddRange(category, donor);
        await db.SaveChangesAsync();
        return new(category.Id, category.Name, donor.Id, donor.Name);
    }

    private async Task<Guid> Donation(Seeded s, string title, DonationStatus status = DonationStatus.Available, double expiresInHours = 4, Guid? categoryId = null)
    {
        await using var db = host.Database.CreateContext();
        var now = DateTimeOffset.UtcNow;
        var donation = new FoodDonation
        {
            DonorOrganizationId = s.DonorOrganizationId, FoodCategoryId = categoryId ?? s.CategoryId, Title = title,
            Description = "Fresh rice trays", Quantity = 12.5m, Unit = QuantityUnit.Kilograms,
            PreparedAtUtc = now.AddHours(expiresInHours - 6), ExpiresAtUtc = now.AddHours(expiresInHours),
            StorageInstructions = "Keep chilled", PickupAddress = "12 Market Street", Status = status, CreatedAtUtc = now
        };
        db.Add(donation);
        await db.SaveChangesAsync();
        return donation.Id;
    }

    private static string Token8() => Guid.NewGuid().ToString("N")[..8];

    // ---- Categories

    [Fact]
    public async Task Categories_require_authentication()
    {
        using var client = Client();
        await Problem(await client.GetAsync("/api/categories"), HttpStatusCode.Unauthorized, "auth.unauthenticated");
    }

    [Theory]
    [InlineData(AppRoles.Donor, OrganizationType.Donor)]
    [InlineData(AppRoles.Beneficiary, OrganizationType.Beneficiary)]
    public async Task Categories_return_only_id_and_name(string role, OrganizationType type)
    {
        var s = await Seed();
        using var client = await SignedIn(role, type);
        var response = await client.GetAsync("/api/categories");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await Json(response)).AsArray();
        var mine = Assert.Single(items, x => x!["id"]!.GetValue<Guid>() == s.CategoryId)!.AsObject();
        Assert.Equal(["id", "name"], mine.Select(p => p.Key));
        Assert.Equal(s.CategoryName, mine["name"]!.GetValue<string>());
        Assert.All(items, x => Assert.Equal(["id", "name"], x!.AsObject().Select(p => p.Key)));
    }

    // ---- Marketplace authorization

    [Fact]
    public async Task Marketplace_rejects_anonymous_callers()
    {
        using var client = Client();
        await Problem(await client.GetAsync("/api/marketplace"), HttpStatusCode.Unauthorized, "auth.unauthenticated");
        await Problem(await client.GetAsync($"/api/marketplace/{Guid.NewGuid()}"), HttpStatusCode.Unauthorized, "auth.unauthenticated");
    }

    [Theory]
    [InlineData(AppRoles.Donor, OrganizationType.Donor)]
    [InlineData(AppRoles.Courier, null)]
    [InlineData(AppRoles.Admin, null)]
    public async Task Marketplace_rejects_other_roles(string role, OrganizationType? type)
    {
        var s = await Seed();
        var id = await Donation(s, "Role check");
        using var client = await SignedIn(role, type);
        await Problem(await client.GetAsync("/api/marketplace"), HttpStatusCode.Forbidden, "auth.forbidden");
        await Problem(await client.GetAsync($"/api/marketplace/{id}"), HttpStatusCode.Forbidden, "auth.forbidden");
    }

    [Theory]
    [InlineData(OrganizationStatus.Pending)]
    [InlineData(OrganizationStatus.Rejected)]
    [InlineData(OrganizationStatus.Suspended)]
    public async Task Marketplace_rejects_beneficiaries_whose_organization_is_not_active(OrganizationStatus status)
    {
        var s = await Seed();
        var id = await Donation(s, "Org state check");
        // Pending/Rejected cannot sign in at all; this is a live session whose organization changes afterwards.
        using var client = Client();
        var email = await User(AppRoles.Beneficiary, OrganizationType.Beneficiary);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/api/auth/login", new { email, password = Password }, await Token(client))).StatusCode);
        await using (var db = host.Database.CreateContext())
        {
            var organizationId = await db.Users.Where(u => u.Email == email).Select(u => u.OrganizationId).SingleAsync();
            (await db.Organizations.SingleAsync(x => x.Id == organizationId)).Status = status;
            await db.SaveChangesAsync();
        }
        await Problem(await client.GetAsync("/api/marketplace"), HttpStatusCode.Forbidden, "organization.not_active");
        await Problem(await client.GetAsync($"/api/marketplace/{id}"), HttpStatusCode.Forbidden, "organization.not_active");
        await Problem(await Claim(client, id), HttpStatusCode.Forbidden, "organization.not_active");
    }

    // ---- Marketplace list

    [Fact]
    public async Task Marketplace_list_returns_a_safe_page_of_available_donations()
    {
        var s = await Seed();
        var tag = Token8();
        var id = await Donation(s, $"Rice {tag}");
        using var client = await Beneficiary();

        var response = await client.GetAsync($"/api/marketplace?search={tag}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        var body = (await Json(response)).AsObject();
        Assert.Equal(["items", "page", "hasPrevious", "hasNext"], body.Select(p => p.Key));
        var item = Assert.Single(body["items"]!.AsArray())!.AsObject();
        Assert.Equal(["id", "title", "category", "quantity", "unit", "preparedAtUtc", "expiresAtUtc", "status", "pickupAddress", "donorName"],
            item.Select(p => p.Key));
        Assert.Equal(id, item["id"]!.GetValue<Guid>());
        Assert.Equal($$"""{"id":"{{s.CategoryId}}","name":"{{s.CategoryName}}"}""", item["category"]!.ToJsonString());
        Assert.Equal("Kilograms", item["unit"]!.GetValue<string>());
        Assert.Equal("Available", item["status"]!.GetValue<string>());
        Assert.Equal(12.5m, item["quantity"]!.GetValue<decimal>());
        Assert.Equal(s.DonorName, item["donorName"]!.GetValue<string>());
        Assert.DoesNotMatch(Private, body.ToJsonString());
        Assert.DoesNotContain(s.DonorOrganizationId.ToString(), body.ToJsonString());
    }

    [Fact]
    public async Task Marketplace_search_matches_title_only()
    {
        var s = await Seed();
        var tag = Token8();
        var title = await Donation(s, $"Soup {tag}");
        var otherId = await Donation(s, "Unrelated title");
        await using (var db = host.Database.CreateContext())
        {
            // The tag appears in every other text field of this one, but never in its title.
            var other = await db.FoodDonations.SingleAsync(x => x.Id == otherId);
            other.Description = other.StorageInstructions = other.PickupAddress = tag;
            await db.SaveChangesAsync();
        }
        using var client = await Beneficiary();
        var items = (await Json(await client.GetAsync($"/api/marketplace?search=%20{tag}%20")))["items"]!.AsArray();
        Assert.Equal([title], items.Select(x => x!["id"]!.GetValue<Guid>()));
    }

    [Fact]
    public async Task Marketplace_filters_by_category_guid()
    {
        var s = await Seed();
        var other = await Seed();
        var tag = Token8();
        var inCategory = await Donation(s, $"Bread {tag}");
        await Donation(s, $"Bread {tag} other", categoryId: other.CategoryId);
        using var client = await Beneficiary();

        var filtered = (await Json(await client.GetAsync($"/api/marketplace?search={tag}&categoryId={s.CategoryId}")))["items"]!.AsArray();
        Assert.Equal([inCategory], filtered.Select(x => x!["id"]!.GetValue<Guid>()));
        Assert.Equal(2, (await Json(await client.GetAsync($"/api/marketplace?search={tag}")))["items"]!.AsArray().Count);
        // A name/slug is not a category id.
        await Problem(await client.GetAsync($"/api/marketplace?categoryId={Uri.EscapeDataString(s.CategoryName)}"), HttpStatusCode.BadRequest, "validation");
    }

    [Fact]
    public async Task Marketplace_paging_follows_the_backend_page_size()
    {
        var s = await Seed();
        var tag = Token8();
        for (var i = 0; i < 21; i++) await Donation(s, $"Page {tag} {i:00}", expiresInHours: 2 + i);
        using var client = await Beneficiary();

        var first = await Json(await client.GetAsync($"/api/marketplace?search={tag}&page=1"));
        Assert.Equal(20, first["items"]!.AsArray().Count);
        Assert.Equal(1, first["page"]!.GetValue<int>());
        Assert.False(first["hasPrevious"]!.GetValue<bool>());
        Assert.True(first["hasNext"]!.GetValue<bool>());

        var second = await Json(await client.GetAsync($"/api/marketplace?search={tag}&page=2"));
        Assert.Equal($"Page {tag} 20", Assert.Single(second["items"]!.AsArray())!["title"]!.GetValue<string>());
        Assert.True(second["hasPrevious"]!.GetValue<bool>());
        Assert.False(second["hasNext"]!.GetValue<bool>());

        // Out-of-range pages are normalized, not errors.
        Assert.Equal(1, (await Json(await client.GetAsync($"/api/marketplace?search={tag}&page=0")))["page"]!.GetValue<int>());
    }

    [Fact]
    public async Task Marketplace_excludes_donations_that_are_not_currently_claimable()
    {
        var s = await Seed();
        var inactiveDonor = await Seed(OrganizationStatus.Suspended);
        var tag = Token8();
        var visible = await Donation(s, $"Visible {tag}");
        await Donation(s, $"Expired {tag}", expiresInHours: -1);
        await Donation(s, $"Draft {tag}", DonationStatus.Draft);
        await Donation(s, $"Claimed {tag}", DonationStatus.Claimed);
        await Donation(s, $"Closed {tag}", DonationStatus.Closed);
        await Donation(inactiveDonor, $"Suspended donor {tag}");
        using var client = await Beneficiary();

        var items = (await Json(await client.GetAsync($"/api/marketplace?search={tag}")))["items"]!.AsArray();
        Assert.Equal([visible], items.Select(x => x!["id"]!.GetValue<Guid>()));
    }

    // ---- Marketplace details

    [Fact]
    public async Task Marketplace_details_return_only_beneficiary_fields()
    {
        var s = await Seed();
        var id = await Donation(s, "Details visible");
        using var client = await Beneficiary();

        var response = await client.GetAsync($"/api/marketplace/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        var body = (await Json(response)).AsObject();
        Assert.Equal(["id", "title", "description", "category", "quantity", "unit", "preparedAtUtc", "expiresAtUtc",
            "storageInstructions", "pickupAddress", "donorName"], body.Select(p => p.Key));
        Assert.Equal("Details visible", body["title"]!.GetValue<string>());
        Assert.Equal("Fresh rice trays", body["description"]!.GetValue<string>());
        Assert.Equal("Keep chilled", body["storageInstructions"]!.GetValue<string>());
        Assert.Equal(s.DonorName, body["donorName"]!.GetValue<string>());
        Assert.Equal(s.CategoryId, body["category"]!["id"]!.GetValue<Guid>());
        Assert.DoesNotMatch(Private, body.ToJsonString());
        Assert.DoesNotContain(s.DonorOrganizationId.ToString(), body.ToJsonString());
    }

    [Fact]
    public async Task Marketplace_details_hide_everything_that_is_not_in_the_marketplace()
    {
        var s = await Seed();
        var inactiveDonor = await Seed(OrganizationStatus.Suspended);
        var hidden = new[]
        {
            Guid.NewGuid(),
            await Donation(s, "Expired", expiresInHours: -1),
            await Donation(s, "Draft", DonationStatus.Draft),
            await Donation(s, "Claimed", DonationStatus.Claimed),
            await Donation(s, "Delivered", DonationStatus.Delivered),
            await Donation(inactiveDonor, "Suspended donor")
        };
        using var client = await Beneficiary();
        foreach (var id in hidden)
            await Problem(await client.GetAsync($"/api/marketplace/{id}"), HttpStatusCode.NotFound, "donation.not_found");
        await Problem(await client.GetAsync("/api/marketplace/not-a-guid"), HttpStatusCode.NotFound, "not_found");
    }

    // ---- Claims

    [Fact]
    public async Task Claim_requires_antiforgery_and_authentication()
    {
        var s = await Seed();
        var id = await Donation(s, "Claim guard");
        using var beneficiary = await Beneficiary();
        await Problem(await Post(beneficiary, "/api/claims", new { donationId = id }, token: null), HttpStatusCode.BadRequest, "antiforgery.invalid");
        await Problem(await Post(beneficiary, "/api/claims", new { donationId = id }, token: "forged"), HttpStatusCode.BadRequest, "antiforgery.invalid");

        using var anonymous = Client();
        await Problem(await Claim(anonymous, id), HttpStatusCode.Unauthorized, "auth.unauthenticated");

        using var donor = await SignedIn(AppRoles.Donor, OrganizationType.Donor);
        await Problem(await Claim(donor, id), HttpStatusCode.Forbidden, "auth.forbidden");

        await using var db = host.Database.CreateContext();
        Assert.Equal(DonationStatus.Available, (await db.FoodDonations.SingleAsync(x => x.Id == id)).Status);
        Assert.False(await db.DonationClaims.AnyAsync(x => x.FoodDonationId == id));
    }

    [Fact]
    public async Task Claim_creates_a_booked_claim_and_hides_the_donation()
    {
        var s = await Seed();
        var id = await Donation(s, "Claim me");
        using var client = await Beneficiary();

        var response = await Claim(client, id);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = (await Json(response)).AsObject();
        Assert.Equal(["claimId"], body.Select(p => p.Key));
        var claimId = body["claimId"]!.GetValue<Guid>();

        await using (var db = host.Database.CreateContext())
        {
            var claim = await db.DonationClaims.SingleAsync(x => x.Id == claimId);
            Assert.Equal(id, claim.FoodDonationId);
            Assert.Equal(ClaimStatus.Booked, claim.Status);
            Assert.Equal(DonationStatus.Claimed, (await db.FoodDonations.SingleAsync(x => x.Id == id)).Status);
        }

        await Problem(await client.GetAsync($"/api/marketplace/{id}"), HttpStatusCode.NotFound, "donation.not_found");
        await Problem(await Claim(client, id), HttpStatusCode.Conflict, "claim.not_available");
    }

    [Fact]
    public async Task Claim_failures_map_to_stable_codes()
    {
        var s = await Seed();
        var inactiveDonor = await Seed(OrganizationStatus.Suspended);
        using var client = await Beneficiary();

        await Problem(await Claim(client, Guid.NewGuid()), HttpStatusCode.NotFound, "donation.not_found");
        await Problem(await Claim(client, await Donation(s, "Draft", DonationStatus.Draft)), HttpStatusCode.Conflict, "claim.not_available");
        await Problem(await Claim(client, await Donation(s, "Expired", expiresInHours: -1)), HttpStatusCode.Conflict, "claim.expired");
        await Problem(await Claim(client, await Donation(inactiveDonor, "Suspended donor")), HttpStatusCode.Conflict, "claim.not_available");
        await Problem(await Post(client, "/api/claims", new { }, await Token(client)), HttpStatusCode.BadRequest, "validation");
        await Problem(await Post(client, "/api/claims", new { donationId = "not-a-guid" }, await Token(client)), HttpStatusCode.BadRequest, "validation");
    }

    [Fact]
    public async Task Concurrent_claims_produce_exactly_one_winner_and_a_controlled_conflict()
    {
        var s = await Seed();
        for (var round = 0; round < 5; round++)
        {
            var id = await Donation(s, $"Race {round}");
            using var a = await Beneficiary();
            using var b = await Beneficiary();
            var (tokenA, tokenB) = (await Token(a), await Token(b));

            var responses = await Task.WhenAll(
                Post(a, "/api/claims", new { donationId = id }, tokenA),
                Post(b, "/api/claims", new { donationId = id }, tokenB));

            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
            var loser = Assert.Single(responses, r => r.StatusCode != HttpStatusCode.Created);
            Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode);
            Assert.Contains((await Json(loser))["code"]!.GetValue<string>(), new[] { "claim.conflict", "claim.not_available" });

            await using var db = host.Database.CreateContext();
            Assert.Equal(1, await db.DonationClaims.CountAsync(x => x.FoodDonationId == id));
        }
    }
}
