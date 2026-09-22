using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FoodLoop.Application.Identity;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FoodLoop.Foundation.Tests;

// R6.4–R6.7 workspace API: donations, organization, claims, admin, courier and handover over real HTTP, cookie and antiforgery.
public sealed class WorkspaceApiTests(MarketplaceApiHost host) : IClassFixture<MarketplaceApiHost>
{
    private const string Password = MarketplaceApiHost.Password;
    // Nothing identifying another party, no concurrency/security internals, no audit payload.
    private const string Private = "(?i)organizationid|userid|courierid|rowversion|tokenhash|details\"|passwordhash|securitystamp|@r64\\.local";

    private sealed record Session(HttpClient Client, string Email, Guid? OrganizationId);

    private HttpClient Client() => host.Factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

    private async Task<Session> SignIn(string role, OrganizationType? type = null, OrganizationStatus status = OrganizationStatus.Active)
    {
        Guid? organizationId = null;
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@r64.local";
        await using (var scope = host.Factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = email, Email = email, DisplayName = "R64 " + role };
            if (type is { } t)
                user.Organization = new Organization { Name = $"R64 {t} {Guid.NewGuid():N}", LicenseNumber = Guid.NewGuid().ToString("N"), Type = t, Status = status, Address = "1 Test Road" };
            Assert.True((await users.CreateAsync(user, Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
            organizationId = user.OrganizationId;
        }
        var client = Client();
        Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Post, "/api/auth/login", new { email, password = Password })).StatusCode);
        return new(client, email, organizationId);
    }

    private Task<Session> Donor() => SignIn(AppRoles.Donor, OrganizationType.Donor);
    private Task<Session> Beneficiary() => SignIn(AppRoles.Beneficiary, OrganizationType.Beneficiary);

    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string url, object? body = null, bool token = true)
    {
        using var request = new HttpRequestMessage(method, url) { Content = body is null ? null : JsonContent.Create(body) };
        if (token) request.Headers.Add("X-XSRF-TOKEN", (await client.GetFromJsonAsync<JsonObject>("/api/auth/antiforgery"))!["token"]!.GetValue<string>());
        return await client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> Post(Session s, string url, object? body = null) => Send(s.Client, HttpMethod.Post, url, body);

    private static async Task<JsonNode> Ok(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(status == response.StatusCode, $"{response.StatusCode}: {text}");
        Assert.DoesNotMatch(Private, text);
        return JsonNode.Parse(text)!;
    }

    private static async Task Problem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(status == response.StatusCode, $"{response.StatusCode}: {text}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(code, JsonNode.Parse(text)!["code"]!.GetValue<string>());
        Assert.DoesNotMatch("(?i)exception|stack|sql|\\.cs\\b", text);
    }

    private async Task<Guid> Category()
    {
        await using var db = host.Database.CreateContext();
        var category = new FoodCategory { Name = "R64 category " + Guid.NewGuid().ToString("N")[..8] };
        db.Add(category);
        await db.SaveChangesAsync();
        return category.Id;
    }

    private static object DonationBody(Guid categoryId, string title = "R64 rice trays", string? version = null) => new
    {
        categoryId, title, description = "Fresh rice", quantity = 12, unit = "Meals",
        preparedAt = DateTimeOffset.UtcNow.AddHours(-1), expiresAt = DateTimeOffset.UtcNow.AddHours(6),
        storageInstructions = "Keep chilled", pickupAddress = "12 Market Street", version
    };

    private async Task<Guid> CreateDonation(Session donor, string title = "R64 rice trays")
    {
        var response = await Post(donor, "/api/donations", DonationBody(await Category(), title));
        var id = (await Ok(response, HttpStatusCode.Created))["id"]!.GetValue<Guid>();
        Assert.Equal($"/api/donations/{id}", response.Headers.Location!.OriginalString);
        return id;
    }

    private async Task<Guid> PublishedDonation(Session donor)
    {
        var id = await CreateDonation(donor);
        Assert.Equal(HttpStatusCode.NoContent, (await Post(donor, $"/api/donations/{id}/publish")).StatusCode);
        return id;
    }

    private async Task<Guid> Claim(Session beneficiary, Guid donationId)
    {
        var response = await Post(beneficiary, "/api/claims", new { donationId });
        var claimId = (await Ok(response, HttpStatusCode.Created))["claimId"]!.GetValue<Guid>();
        Assert.Equal($"/api/claims/{claimId}", response.Headers.Location!.OriginalString);
        return claimId;
    }

    private async Task<Guid> CourierId(Session courier)
    {
        await using var db = host.Database.CreateContext();
        return await db.Users.Where(u => u.Email == courier.Email).Select(u => u.Id).SingleAsync();
    }

    // ---- Full lifecycle

    [Fact]
    public async Task Full_lifecycle_runs_end_to_end_over_the_api()
    {
        var admin = await SignIn(AppRoles.Admin);
        var donor = await Donor();
        var beneficiary = await Beneficiary();
        var courier = await SignIn(AppRoles.Courier);

        // Draft, edit with version, publish.
        var donationId = await CreateDonation(donor);
        var draft = await Ok(await donor.Client.GetAsync($"/api/donations/{donationId}"));
        Assert.Equal("Draft", draft["status"]!.GetValue<string>());
        Assert.True(draft["canEdit"]!.GetValue<bool>());
        Assert.True(draft["canPublish"]!.GetValue<bool>());
        var edit = await Ok(await donor.Client.GetAsync($"/api/donations/{donationId}/edit"));
        var version = edit["version"]!.GetValue<string>();
        Assert.Equal(HttpStatusCode.NoContent, (await Send(donor.Client, HttpMethod.Put, $"/api/donations/{donationId}",
            DonationBody(edit["categoryId"]!.GetValue<Guid>(), "R64 edited trays", version))).StatusCode);
        await Problem(await Send(donor.Client, HttpMethod.Put, $"/api/donations/{donationId}",
            DonationBody(edit["categoryId"]!.GetValue<Guid>(), "R64 stale", version)), HttpStatusCode.Conflict, "donation.stale");
        Assert.Equal(HttpStatusCode.NoContent, (await Post(donor, $"/api/donations/{donationId}/publish")).StatusCode);
        await Problem(await Post(donor, $"/api/donations/{donationId}/publish"), HttpStatusCode.Conflict, "donation.invalid_state");
        await Problem(await donor.Client.GetAsync($"/api/donations/{donationId}/edit"), HttpStatusCode.Conflict, "donation.not_editable");
        var published = Assert.Single((await Ok(await donor.Client.GetAsync("/api/donations?status=Available"))).AsArray(),
            x => x!["id"]!.GetValue<Guid>() == donationId)!;
        Assert.Equal("R64 edited trays", published["title"]!.GetValue<string>());
        Assert.False(published["canEdit"]!.GetValue<bool>());
        Assert.DoesNotContain("version", published.AsObject().Select(p => p.Key));

        // Beneficiary sees and claims it.
        Assert.Contains((await Ok(await beneficiary.Client.GetAsync("/api/marketplace?search=R64 edited"))).AsObject()["items"]!.AsArray(),
            x => x!["id"]!.GetValue<Guid>() == donationId);
        var claimId = await Claim(beneficiary, donationId);
        var list = await Ok(await beneficiary.Client.GetAsync("/api/claims?page=1&pageSize=20"));
        Assert.False(list["hasNext"]!.GetValue<bool>());
        Assert.Contains(list["items"]!.AsArray(), x => x!["claimId"]!.GetValue<Guid>() == claimId && x["canCancel"]!.GetValue<bool>());

        // Admin assigns a courier through safe projections.
        var courierId = await CourierId(courier);
        var couriers = (await Ok(await admin.Client.GetAsync("/api/admin/couriers"))).AsArray();
        Assert.All(couriers, x => Assert.Equal(["id", "name"], x!.AsObject().Select(p => p.Key)));
        Assert.Contains(couriers, x => x!["id"]!.GetValue<Guid>() == courierId && x["name"]!.GetValue<string>() == "R64 Courier");
        var assignable = Assert.Single((await Ok(await admin.Client.GetAsync("/api/admin/claims/assignable"))).AsArray(),
            x => x!["claimId"]!.GetValue<Guid>() == claimId)!;
        Assert.False(assignable["hasCourier"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.NoContent, (await Post(admin, $"/api/admin/claims/{claimId}/courier", new { courierUserId = courierId })).StatusCode);
        await Problem(await Post(beneficiary, $"/api/claims/{claimId}/cancel"), HttpStatusCode.Conflict, "claim.not_cancellable");

        // Pickup: a second issue revokes the first code; only the latest verifies.
        var donorTask = Assert.Single((await Ok(await donor.Client.GetAsync("/api/handover/tasks"))).AsArray(), x => x!["claimId"]!.GetValue<Guid>() == claimId)!;
        Assert.Equal(["claimId", "donationTitle", "status", "type", "canIssue"], donorTask.AsObject().Select(p => p.Key));
        Assert.True(donorTask["canIssue"]!.GetValue<bool>());
        var first = await Post(donor, $"/api/handover/{claimId}/codes", new { type = "Pickup" });
        Assert.True(first.Headers.CacheControl?.NoStore);
        var firstCode = (await Ok(first))["code"]!.GetValue<string>();
        var second = await Ok(await Post(donor, $"/api/handover/{claimId}/codes", new { type = "Pickup" }));
        Assert.Equal(["code", "type", "expiresAtUtc", "qrSvg"], second.AsObject().Select(p => p.Key));
        Assert.Contains("<svg", second["qrSvg"]!.GetValue<string>());
        var pickupCode = second["code"]!.GetValue<string>();
        Assert.Equal(64, pickupCode.Length);
        await using (var db = host.Database.CreateContext())
            Assert.DoesNotContain(await db.QrVerificationTokens.Where(x => x.DonationClaimId == claimId).Select(x => x.TokenHash).ToListAsync(),
                h => h == firstCode || h == pickupCode);
        await Problem(await Post(beneficiary, $"/api/handover/{claimId}/codes", new { type = "Pickup" }), HttpStatusCode.NotFound, "claim.not_found");

        var task = await Ok(await courier.Client.GetAsync($"/api/courier/tasks/{claimId}"));
        Assert.Equal("VerifyPickup", task["nextStep"]!.GetValue<string>());
        Assert.Empty(task["evidence"]!.AsArray());
        await Problem(await Post(courier, $"/api/courier/tasks/{claimId}/verify", new { type = "Pickup", code = firstCode }), HttpStatusCode.BadRequest, "handover.invalid_code");
        await Problem(await Post(courier, $"/api/courier/tasks/{claimId}/verify", new { type = "Delivery", code = pickupCode }), HttpStatusCode.Conflict, "handover.invalid_state");
        Assert.Equal(HttpStatusCode.NoContent, (await Post(courier, $"/api/courier/tasks/{claimId}/verify",
            new { type = "Pickup", code = $" {pickupCode[..32]} {pickupCode[32..]}\n" })).StatusCode);
        await Problem(await Post(courier, $"/api/courier/tasks/{claimId}/verify", new { type = "Pickup", code = pickupCode }), HttpStatusCode.Conflict, "handover.invalid_state");

        // Delivery closes the claim.
        var deliveryCode = (await Ok(await Post(beneficiary, $"/api/handover/{claimId}/codes", new { type = "Delivery" })))["code"]!.GetValue<string>();
        var listed = (await Ok(await courier.Client.GetAsync("/api/courier/tasks"))).AsArray().Single(x => x!["claimId"]!.GetValue<Guid>() == claimId)!;
        Assert.Equal(["claimId", "donationTitle", "status", "nextStep", "donorName", "beneficiaryName", "pickupAddress", "expiresAtUtc"],
            listed.AsObject().Select(p => p.Key));
        Assert.Equal("VerifyDelivery", listed["nextStep"]!.GetValue<string>());
        Assert.Equal("12 Market Street", listed["pickupAddress"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.NoContent, (await Post(courier, $"/api/courier/tasks/{claimId}/verify", new { type = "Delivery", code = deliveryCode })).StatusCode);

        var done = await Ok(await courier.Client.GetAsync($"/api/courier/tasks/{claimId}"));
        Assert.Equal("Completed", done["nextStep"]!.GetValue<string>());
        Assert.Equal(["Pickup", "Delivery"], done["evidence"]!.AsArray().Select(x => x!["type"]!.GetValue<string>()));
        var details = await Ok(await beneficiary.Client.GetAsync($"/api/claims/{claimId}"));
        Assert.Equal("Closed", details["status"]!.GetValue<string>());
        Assert.False(details["canCancel"]!.GetValue<bool>());
        Assert.Equal("R64 Courier", details["courierDisplayName"]!.GetValue<string>());
        Assert.Equal(["Claimed", "CourierAssigned", "PickupVerified", "DeliveryVerified", "Closed"],
            details["timeline"]!.AsArray().Select(x => x!["kind"]!.GetValue<string>()));
        Assert.Equal("Closed", (await Ok(await donor.Client.GetAsync($"/api/donations/{donationId}")))["status"]!.GetValue<string>());

        // Admin audit shows the lifecycle with names only.
        var audit = await Ok(await admin.Client.GetAsync($"/api/admin/audit?actor={Uri.EscapeDataString("R64 Courier")}"));
        var entries = audit["items"]!.AsArray();
        Assert.All(entries, x => Assert.Equal(["id", "action", "actorName", "entityType", "entityId", "timestampUtc"], x!.AsObject().Select(p => p.Key)));
        Assert.Contains(entries, x => x!["action"]!.GetValue<string>() == "DeliveryVerified" && x["entityId"]!.GetValue<Guid>() == claimId);
        var dashboard = await Ok(await admin.Client.GetAsync("/api/admin/dashboard"));
        Assert.Equal(["pendingOrganizations", "availableDonations", "closedDeliveries", "cancelledClaims", "expiredDonations"], dashboard.AsObject().Select(p => p.Key));
    }

    // ---- Authorization

    [Theory]
    [InlineData("/api/donations", AppRoles.Beneficiary)]
    [InlineData("/api/claims", AppRoles.Donor)]
    [InlineData("/api/admin/dashboard", AppRoles.Donor)]
    [InlineData("/api/admin/audit", AppRoles.Courier)]
    [InlineData("/api/courier/tasks", AppRoles.Beneficiary)]
    [InlineData("/api/handover/tasks", AppRoles.Courier)]
    [InlineData("/api/handover/tasks", AppRoles.Admin)]
    public async Task Endpoints_reject_the_wrong_role(string url, string role)
    {
        var type = role == AppRoles.Donor ? OrganizationType.Donor : role == AppRoles.Beneficiary ? OrganizationType.Beneficiary : (OrganizationType?)null;
        var session = await SignIn(role, type);
        await Problem(await session.Client.GetAsync(url), HttpStatusCode.Forbidden, "auth.forbidden");
    }

    [Theory]
    [InlineData("/api/donations")]
    [InlineData("/api/organization")]
    [InlineData("/api/claims")]
    [InlineData("/api/admin/organizations")]
    [InlineData("/api/courier/tasks")]
    [InlineData("/api/handover/tasks")]
    public async Task Endpoints_require_authentication(string url)
    {
        using var client = Client();
        await Problem(await client.GetAsync(url), HttpStatusCode.Unauthorized, "auth.unauthenticated");
    }

    [Fact]
    public async Task Unsafe_endpoints_require_the_antiforgery_header()
    {
        var donor = await Donor();
        var beneficiary = await Beneficiary();
        var admin = await SignIn(AppRoles.Admin);
        var courier = await SignIn(AppRoles.Courier);
        var id = Guid.NewGuid();
        await Problem(await Send(donor.Client, HttpMethod.Post, "/api/donations", DonationBody(Guid.NewGuid()), token: false), HttpStatusCode.BadRequest, "antiforgery.invalid");
        await Problem(await Send(donor.Client, HttpMethod.Put, $"/api/donations/{id}", DonationBody(Guid.NewGuid()), token: false), HttpStatusCode.BadRequest, "antiforgery.invalid");
        await Problem(await Send(donor.Client, HttpMethod.Post, $"/api/donations/{id}/publish", token: false), HttpStatusCode.BadRequest, "antiforgery.invalid");
        await Problem(await Send(donor.Client, HttpMethod.Post, $"/api/handover/{id}/codes", new { type = "Pickup" }, token: false), HttpStatusCode.BadRequest, "antiforgery.invalid");
        await Problem(await Send(beneficiary.Client, HttpMethod.Post, $"/api/claims/{id}/cancel", token: false), HttpStatusCode.BadRequest, "antiforgery.invalid");
        await Problem(await Send(admin.Client, HttpMethod.Post, $"/api/admin/organizations/{id}/approve", token: false), HttpStatusCode.BadRequest, "antiforgery.invalid");
        await Problem(await Send(admin.Client, HttpMethod.Post, $"/api/admin/claims/{id}/courier", new { courierUserId = id }, token: false), HttpStatusCode.BadRequest, "antiforgery.invalid");
        await Problem(await Send(courier.Client, HttpMethod.Post, $"/api/courier/tasks/{id}/verify", new { type = "Pickup", code = "x" }, token: false), HttpStatusCode.BadRequest, "antiforgery.invalid");
    }

    // ---- Ownership

    [Fact]
    public async Task Foreign_records_are_indistinguishable_from_missing_ones()
    {
        var donor = await Donor();
        var otherDonor = await Donor();
        var beneficiary = await Beneficiary();
        var otherBeneficiary = await Beneficiary();
        var admin = await SignIn(AppRoles.Admin);
        var courier = await SignIn(AppRoles.Courier);
        var otherCourier = await SignIn(AppRoles.Courier);

        var draftId = await CreateDonation(donor);
        await Problem(await otherDonor.Client.GetAsync($"/api/donations/{draftId}"), HttpStatusCode.NotFound, "donation.not_found");
        await Problem(await otherDonor.Client.GetAsync($"/api/donations/{draftId}/edit"), HttpStatusCode.NotFound, "donation.not_found");
        await Problem(await Post(otherDonor, $"/api/donations/{draftId}/publish"), HttpStatusCode.NotFound, "donation.not_found");
        Assert.DoesNotContain((await Ok(await otherDonor.Client.GetAsync("/api/donations"))).AsArray(), x => x!["id"]!.GetValue<Guid>() == draftId);

        var claimId = await Claim(beneficiary, await PublishedDonation(donor));
        await Problem(await otherBeneficiary.Client.GetAsync($"/api/claims/{claimId}"), HttpStatusCode.NotFound, "claim.not_found");
        await Problem(await Post(otherBeneficiary, $"/api/claims/{claimId}/cancel"), HttpStatusCode.NotFound, "claim.not_found");

        Assert.Equal(HttpStatusCode.NoContent, (await Post(admin, $"/api/admin/claims/{claimId}/courier", new { courierUserId = await CourierId(courier) })).StatusCode);
        await Problem(await Post(otherDonor, $"/api/handover/{claimId}/codes", new { type = "Pickup" }), HttpStatusCode.NotFound, "claim.not_found");
        await Problem(await otherCourier.Client.GetAsync($"/api/courier/tasks/{claimId}"), HttpStatusCode.NotFound, "task.not_found");
        await Problem(await Post(otherCourier, $"/api/courier/tasks/{claimId}/verify", new { type = "Pickup", code = new string('A', 64) }), HttpStatusCode.NotFound, "task.not_found");
        Assert.DoesNotContain((await Ok(await otherCourier.Client.GetAsync("/api/courier/tasks"))).AsArray(), x => x!["claimId"]!.GetValue<Guid>() == claimId);
    }

    // ---- Claims

    [Fact]
    public async Task Cancel_returns_the_donation_and_a_repeat_is_a_conflict()
    {
        var donor = await Donor();
        var beneficiary = await Beneficiary();
        var donationId = await PublishedDonation(donor);
        var claimId = await Claim(beneficiary, donationId);
        Assert.Equal(HttpStatusCode.NoContent, (await Post(beneficiary, $"/api/claims/{claimId}/cancel")).StatusCode);
        await Problem(await Post(beneficiary, $"/api/claims/{claimId}/cancel"), HttpStatusCode.Conflict, "claim.not_cancellable");
        var details = await Ok(await beneficiary.Client.GetAsync($"/api/claims/{claimId}"));
        Assert.Equal("Cancelled", details["status"]!.GetValue<string>());
        Assert.Equal(["Claimed", "Cancelled"], details["timeline"]!.AsArray().Select(x => x!["kind"]!.GetValue<string>()));
        Assert.Equal("Available", (await Ok(await donor.Client.GetAsync($"/api/donations/{donationId}")))["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task My_claims_pages_with_has_next_and_no_total()
    {
        var donor = await Donor();
        var beneficiary = await Beneficiary();
        for (var i = 0; i < 3; i++) await Claim(beneficiary, await PublishedDonation(donor));
        var first = await Ok(await beneficiary.Client.GetAsync("/api/claims?page=1&pageSize=2"));
        Assert.Equal(["items", "page", "hasNext"], first.AsObject().Select(p => p.Key));
        Assert.Equal(2, first["items"]!.AsArray().Count);
        Assert.True(first["hasNext"]!.GetValue<bool>());
        var last = await Ok(await beneficiary.Client.GetAsync("/api/claims?page=2&pageSize=2"));
        Assert.Single(last["items"]!.AsArray());
        Assert.False(last["hasNext"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Suspended_beneficiary_keeps_read_only_history()
    {
        var donor = await Donor();
        var beneficiary = await Beneficiary();
        var claimId = await Claim(beneficiary, await PublishedDonation(donor));
        await using (var db = host.Database.CreateContext())
        {
            (await db.Organizations.SingleAsync(x => x.Id == beneficiary.OrganizationId)).Status = OrganizationStatus.Suspended;
            await db.SaveChangesAsync();
        }
        Assert.False((await Ok(await beneficiary.Client.GetAsync($"/api/claims/{claimId}")))["canCancel"]!.GetValue<bool>());
        Assert.True((await Ok(await beneficiary.Client.GetAsync("/api/organization")))["isReadOnly"]!.GetValue<bool>());
        await Problem(await Post(beneficiary, $"/api/claims/{claimId}/cancel"), HttpStatusCode.Forbidden, "organization.not_active");
    }

    // ---- Donations / organization

    [Fact]
    public async Task Donation_validation_and_organization_profile_are_safe()
    {
        var donor = await Donor();
        await Problem(await Post(donor, "/api/donations", new { title = "No category" }), HttpStatusCode.BadRequest, "validation");
        var invalid = await Post(donor, "/api/donations", DonationBody(await Category(), new string('x', 201)));
        await Problem(invalid, HttpStatusCode.BadRequest, "donation.invalid");
        await Problem(await Post(donor, "/api/donations", DonationBody(Guid.NewGuid())), HttpStatusCode.BadRequest, "donation.invalid");

        var org = await Ok(await donor.Client.GetAsync("/api/organization"));
        Assert.Equal(["name", "address", "licenseNumber", "type", "status", "createdAt", "isReadOnly"], org.AsObject().Select(p => p.Key));
        Assert.Equal("Donor", org["type"]!.GetValue<string>());
        Assert.False(org["isReadOnly"]!.GetValue<bool>());
        await Problem(await (await SignIn(AppRoles.Courier)).Client.GetAsync("/api/organization"), HttpStatusCode.NotFound, "organization.not_found");
    }

    // ---- Admin

    [Fact]
    public async Task Admin_approves_suspends_and_reactivates_through_typed_outcomes()
    {
        var admin = await SignIn(AppRoles.Admin);
        Guid pendingId;
        await using (var db = host.Database.CreateContext())
        {
            var pending = new Organization { Name = "R64 pending " + Guid.NewGuid().ToString("N")[..8], LicenseNumber = Guid.NewGuid().ToString("N"), Type = OrganizationType.Donor, Status = OrganizationStatus.Pending };
            db.Add(pending);
            await db.SaveChangesAsync();
            pendingId = pending.Id;
        }
        var row = Assert.Single((await Ok(await admin.Client.GetAsync("/api/admin/organizations/pending"))).AsArray(), x => x!["id"]!.GetValue<Guid>() == pendingId)!;
        Assert.Equal(["id", "name", "type", "licenseNumber", "createdAtUtc"], row.AsObject().Select(p => p.Key));

        Assert.Equal(HttpStatusCode.NoContent, (await Post(admin, $"/api/admin/organizations/{pendingId}/approve")).StatusCode);
        await Problem(await Post(admin, $"/api/admin/organizations/{pendingId}/reject"), HttpStatusCode.Conflict, "organization.invalid_state");
        await Problem(await Post(admin, $"/api/admin/organizations/{pendingId}/reactivate"), HttpStatusCode.Conflict, "organization.invalid_state");
        Assert.Equal(HttpStatusCode.NoContent, (await Post(admin, $"/api/admin/organizations/{pendingId}/suspend")).StatusCode);
        var suspended = await Ok(await admin.Client.GetAsync("/api/admin/organizations?status=Suspended"));
        Assert.Contains(suspended["items"]!.AsArray(), x => x!["id"]!.GetValue<Guid>() == pendingId);
        Assert.Equal(HttpStatusCode.NoContent, (await Post(admin, $"/api/admin/organizations/{pendingId}/reactivate")).StatusCode);
        await Problem(await Post(admin, $"/api/admin/organizations/{Guid.NewGuid()}/approve"), HttpStatusCode.NotFound, "organization.not_found");
        await Problem(await Post(admin, $"/api/admin/claims/{Guid.NewGuid()}/courier", new { courierUserId = Guid.NewGuid() }), HttpStatusCode.NotFound, "claim.not_found");
        await Problem(await admin.Client.GetAsync("/api/admin/audit?fromUtc=2026-02-01T00:00:00Z&toUtc=2026-01-01T00:00:00Z"), HttpStatusCode.BadRequest, "audit.invalid_range");
        var audit = await Ok(await admin.Client.GetAsync("/api/admin/audit?action=OrganizationApproved"));
        Assert.Contains(audit["items"]!.AsArray(), x => x!["entityId"]!.GetValue<Guid>() == pendingId);
        Assert.All(audit["items"]!.AsArray(), x => Assert.Equal("OrganizationApproved", x!["action"]!.GetValue<string>()));
    }

    [Fact]
    public async Task Assigning_a_non_courier_is_rejected()
    {
        var admin = await SignIn(AppRoles.Admin);
        var donor = await Donor();
        var beneficiary = await Beneficiary();
        var claimId = await Claim(beneficiary, await PublishedDonation(donor));
        await Problem(await Post(admin, $"/api/admin/claims/{claimId}/courier", new { courierUserId = Guid.NewGuid() }), HttpStatusCode.BadRequest, "courier.invalid");
        await Problem(await Post(donor, $"/api/handover/{claimId}/codes", new { type = "Pickup" }), HttpStatusCode.Conflict, "handover.not_ready");
    }
}
