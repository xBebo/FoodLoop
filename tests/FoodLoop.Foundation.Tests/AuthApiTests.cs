using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FoodLoop.Application.Identity;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FoodLoop.Foundation.Tests;

// Test-only endpoints, added to the host as an application part: a role-protected API and one that throws with sensitive text.
[ApiController, Route("api/test-only")]
public sealed class TestOnlyApiController : ControllerBase
{
    [HttpGet("admin"), Authorize(Roles = AppRoles.Admin)]
    public IActionResult AdminOnly() => NoContent();

    [HttpGet("boom"), AllowAnonymous]
    public IActionResult Boom() => throw new InvalidOperationException(@"SELECT PasswordHash FROM AspNetUsers failed at C:\internal\secret.cs");
}

// One migrated database and one real host (Identity cookies, antiforgery, JSON) shared by the R6.2 auth API tests.
public sealed class AuthApiHost : IAsyncLifetime
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
            .ConfigureTestServices(s => s.AddControllers().AddApplicationPart(typeof(TestOnlyApiController).Assembly)));
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

public sealed class AuthApiTests(AuthApiHost host) : IClassFixture<AuthApiHost>
{
    private const string Password = AuthApiHost.Password;

    private HttpClient Client() => host.Factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

    private async Task<string> User(string role, OrganizationType? type = null, OrganizationStatus status = OrganizationStatus.Active)
    {
        await using var scope = host.Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@r62.local";
        var user = new ApplicationUser { UserName = email, Email = email, DisplayName = "R62 " + role };
        if (type is { } t)
            user.Organization = new Organization { Name = $"R62 {t} kitchen", LicenseNumber = Guid.NewGuid().ToString("N"), Type = t, Status = status };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        return email;
    }

    private static async Task<string> Token(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonObject>("/api/auth/antiforgery"))!["token"]!.GetValue<string>();

    // token: null sends no header; otherwise the given token (use Token() for a fresh one bound to the current identity).
    private static async Task<HttpResponseMessage> Post(HttpClient client, string url, object? body, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = body is null ? null : JsonContent.Create(body) };
        if (token is not null) request.Headers.Add("X-XSRF-TOKEN", token);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> Login(HttpClient client, string email, string password = Password) =>
        await Post(client, "/api/auth/login", new { email, password }, await Token(client));

    private static async Task<JsonObject> Json(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();

    private static async Task<JsonObject> Session(HttpClient client) => (await client.GetFromJsonAsync<JsonObject>("/api/auth/session"))!;

    // Asserts the sanitized ProblemDetails shape and returns it without the per-request traceId, for comparing contracts.
    private static async Task<string> Problem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Null(response.Headers.Location);
        var body = await Json(response);
        Assert.Equal((int)status, body["status"]!.GetValue<int>());
        Assert.Equal(code, body["code"]!.GetValue<string>());
        Assert.False(string.IsNullOrEmpty(body["title"]?.GetValue<string>()));
        Assert.False(string.IsNullOrEmpty(body["type"]?.GetValue<string>()));
        Assert.False(string.IsNullOrEmpty(body["traceId"]?.GetValue<string>()));
        Assert.DoesNotMatch("(?i)exception|stack|sql|select |C:\\\\|\\.cs\\b|Pending|Rejected|Suspended|FoodLoop\\.", body.ToJsonString());
        body.Remove("traceId");
        return body.ToJsonString();
    }

    // ---- Antiforgery

    [Fact]
    public async Task Antiforgery_token_is_issued_anonymously_and_never_cached()
    {
        using var client = Client();
        var response = await client.GetAsync("/api/auth/antiforgery");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".AspNetCore.Antiforgery.", StringComparison.Ordinal));
        var body = await Json(response);
        Assert.Equal(["token"], body.Select(p => p.Key));
        Assert.False(string.IsNullOrEmpty(body["token"]!.GetValue<string>()));
    }

    [Fact]
    public async Task Unsafe_api_request_without_a_valid_token_is_rejected_before_the_endpoint()
    {
        using var client = Client();
        var email = await User(AppRoles.Donor, OrganizationType.Donor);
        await Problem(await Post(client, "/api/auth/login", new { email, password = Password }, token: null), HttpStatusCode.BadRequest, "antiforgery.invalid");
        await Problem(await Post(client, "/api/auth/login", new { email, password = Password }, token: "forged"), HttpStatusCode.BadRequest, "antiforgery.invalid");
        Assert.False((await Session(client))["isAuthenticated"]!.GetValue<bool>());

        // Same request with a real token reaches the endpoint.
        Assert.Equal(HttpStatusCode.OK, (await Login(client, email)).StatusCode);
    }

    [Fact]
    public async Task Antiforgery_token_is_identity_bound_across_login_and_logout()
    {
        using var client = Client();
        var email = await User(AppRoles.Donor, OrganizationType.Donor);
        var anonymous = await Token(client);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/api/auth/login", new { email, password = Password }, anonymous)).StatusCode);

        // The anonymous token no longer matches the signed-in identity; the SPA must fetch a new one after login.
        await Problem(await Post(client, "/api/auth/logout", null, anonymous), HttpStatusCode.BadRequest, "antiforgery.invalid");
        var authenticated = await Token(client);
        Assert.NotEqual(anonymous, authenticated);
        Assert.Equal(HttpStatusCode.NoContent, (await Post(client, "/api/auth/logout", null, authenticated)).StatusCode);

        // ...and after logout the authenticated token is stale in turn.
        await Problem(await Post(client, "/api/auth/login", new { email, password = Password }, authenticated), HttpStatusCode.BadRequest, "antiforgery.invalid");
        Assert.Equal(HttpStatusCode.OK, (await Login(client, email)).StatusCode);
    }

    // ---- Session

    [Fact]
    public async Task Anonymous_session_reports_only_that_it_is_anonymous()
    {
        using var client = Client();
        var response = await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        Assert.Equal("""{"isAuthenticated":false}""", (await Json(response)).ToJsonString());
    }

    [Theory]
    [InlineData(AppRoles.Donor, OrganizationType.Donor)]
    [InlineData(AppRoles.Beneficiary, OrganizationType.Beneficiary)]
    [InlineData(AppRoles.Courier, null)]
    [InlineData(AppRoles.Admin, null)]
    public async Task Login_returns_the_session_and_the_cookie_restores_it(string role, OrganizationType? type)
    {
        using var client = Client();
        var email = await User(role, type);
        var login = await Login(client, email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", cookie, StringComparison.OrdinalIgnoreCase); // Non-persistent: no remember-me.

        var session = await Session(client);
        Assert.Equal((await Json(login)).ToJsonString(), session.ToJsonString());
        Assert.Equal(["isAuthenticated", "displayName", "roles", "organization"], session.Select(p => p.Key));
        Assert.True(session["isAuthenticated"]!.GetValue<bool>());
        Assert.Equal("R62 " + role, session["displayName"]!.GetValue<string>());
        Assert.Equal([role], session["roles"]!.AsArray().Select(r => r!.GetValue<string>()));
        if (type is null) Assert.Null(session["organization"]);
        else Assert.Equal($$"""{"name":"R62 {{type}} kitchen","type":"{{type}}","status":"Active"}""", session["organization"]!.ToJsonString());
        // No ids, email or security data.
        Assert.DoesNotContain(email, session.ToJsonString());
        Assert.DoesNotMatch("(?i)\"id\"|guid|stamp|claim|[0-9a-f]{8}-[0-9a-f]{4}-", session.ToJsonString());
    }

    // ---- Login

    [Fact]
    public async Task Unknown_account_and_wrong_password_share_one_contract_whatever_the_organization_status()
    {
        using var client = Client();
        var active = await User(AppRoles.Donor, OrganizationType.Donor);
        var pending = await User(AppRoles.Donor, OrganizationType.Donor, OrganizationStatus.Pending);
        var rejected = await User(AppRoles.Beneficiary, OrganizationType.Beneficiary, OrganizationStatus.Rejected);
        var suspended = await User(AppRoles.Donor, OrganizationType.Donor, OrganizationStatus.Suspended);

        var unknown = await Problem(await Login(client, "nobody-" + Guid.NewGuid() + "@r62.local"), HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        foreach (var email in new[] { active, pending, rejected, suspended, active.ToUpperInvariant() })
            Assert.Equal(unknown, await Problem(await Login(client, email, "Wrong-Password!1"), HttpStatusCode.Unauthorized, "auth.invalid_credentials"));
        Assert.False((await Session(client))["isAuthenticated"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Correct_password_on_a_blocked_organization_is_one_generic_403_without_a_cookie()
    {
        var blocked = new[]
        {
            await User(AppRoles.Donor, OrganizationType.Donor, OrganizationStatus.Pending),
            await User(AppRoles.Beneficiary, OrganizationType.Beneficiary, OrganizationStatus.Pending),
            await User(AppRoles.Donor, OrganizationType.Donor, OrganizationStatus.Rejected),
            await User(AppRoles.Beneficiary, OrganizationType.Beneficiary, OrganizationStatus.Rejected),
            await User(AppRoles.Donor, OrganizationType.Donor, OrganizationStatus.Suspended),
        };
        var contracts = new HashSet<string>();
        foreach (var email in blocked)
        {
            using var client = Client();
            var response = await Login(client, email);
            contracts.Add(await Problem(response, HttpStatusCode.Forbidden, "auth.account_unavailable"));
            Assert.DoesNotContain(response.Headers.TryGetValues("Set-Cookie", out var c) ? c : [], x => x.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal));
            Assert.False((await Session(client))["isAuthenticated"]!.GetValue<bool>());
        }
        Assert.Single(contracts);
    }

    [Fact]
    public async Task Password_only_login_cannot_bypass_two_factor_authentication()
    {
        using var client = Client();
        var email = await User(AppRoles.Admin);
        await using (var scope = host.Factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByEmailAsync(email))!;
            Assert.True((await users.SetTwoFactorEnabledAsync(user, true)).Succeeded);
        }

        var response = await Login(client, email);
        await Problem(response, HttpStatusCode.Forbidden, "auth.account_unavailable");
        Assert.DoesNotContain(response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : [],
            cookie => cookie.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal));
        Assert.False((await Session(client))["isAuthenticated"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Suspended_beneficiary_keeps_read_only_sign_in_and_sees_its_own_status()
    {
        using var client = Client();
        var login = await Login(client, await User(AppRoles.Beneficiary, OrganizationType.Beneficiary, OrganizationStatus.Suspended));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal("Suspended", (await Json(login))["organization"]!["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Login_with_missing_fields_is_a_validation_problem()
    {
        using var client = Client();
        var response = await Post(client, "/api/auth/login", new { email = "someone@r62.local" }, await Token(client));
        await Problem(response, HttpStatusCode.BadRequest, "validation");
        await Problem(await Post(client, "/api/auth/login", "not an object", await Token(client)), HttpStatusCode.BadRequest, "validation");
    }

    // ---- Logout

    [Fact]
    public async Task Logout_requires_authentication_and_clears_the_session_without_a_redirect()
    {
        using var client = Client();
        await Problem(await Post(client, "/api/auth/logout", null, await Token(client)), HttpStatusCode.Unauthorized, "auth.unauthenticated");

        Assert.Equal(HttpStatusCode.OK, (await Login(client, await User(AppRoles.Courier))).StatusCode);
        var logout = await Post(client, "/api/auth/logout", null, await Token(client));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Null(logout.Headers.Location);
        Assert.Empty(await logout.Content.ReadAsByteArrayAsync());
        Assert.Contains(logout.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".AspNetCore.Identity.Application=;", StringComparison.Ordinal));
        Assert.False((await Session(client))["isAuthenticated"]!.GetValue<bool>());
        await Problem(await client.GetAsync("/api/test-only/admin"), HttpStatusCode.Unauthorized, "auth.unauthenticated");
    }

    // ---- Registration

    private static object Registration(string type, string? license = null, string? email = null, string password = Password) => new
    {
        organizationName = "R62 registration",
        licenseNumber = license ?? Guid.NewGuid().ToString("N"),
        organizationType = type,
        email = email ?? Guid.NewGuid() + "@r62.local",
        password
    };

    [Theory]
    [InlineData("Donor")]
    [InlineData("Beneficiary")]
    public async Task Registration_creates_a_pending_organization_that_cannot_sign_in_yet(string type)
    {
        using var client = Client();
        var email = Guid.NewGuid() + "@r62.local";
        var response = await Post(client, "/api/auth/register", Registration(type, email: email), await Token(client));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False((await Session(client))["isAuthenticated"]!.GetValue<bool>()); // Never signed in by registering.

        await using (var db = host.Database.CreateContext())
        {
            var user = await db.Users.Include(x => x.Organization).SingleAsync(x => x.Email == email);
            Assert.Equal((OrganizationStatus.Pending, Enum.Parse<OrganizationType>(type)), (user.Organization!.Status, user.Organization.Type));
            var roles = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where ur.UserId == user.Id select r.Name).ToListAsync();
            Assert.Equal([type], roles);
        }
        await Problem(await Login(client, email), HttpStatusCode.Forbidden, "auth.account_unavailable");
    }

    [Theory]
    [InlineData("Courier")]
    [InlineData("Admin")]
    [InlineData("0")]
    [InlineData("")]
    public async Task Registration_only_accepts_donor_or_beneficiary(string type)
    {
        using var client = Client();
        var email = Guid.NewGuid() + "@r62.local";
        var body = type == "0"
            ? new { organizationName = "R62", licenseNumber = Guid.NewGuid().ToString("N"), organizationType = (object)0, email, password = Password }
            : Registration(type, email: email);
        await Problem(await Post(client, "/api/auth/register", body, await Token(client)), HttpStatusCode.BadRequest, "validation");
        await using var db = host.Database.CreateContext();
        Assert.False(await db.Users.AnyAsync(x => x.Email == email));
    }

    [Fact]
    public async Task Registration_conflicts_and_weak_passwords_map_to_safe_problems()
    {
        using var client = Client();
        var license = Guid.NewGuid().ToString("N");
        var email = Guid.NewGuid() + "@r62.local";
        Assert.Equal(HttpStatusCode.Created, (await Post(client, "/api/auth/register", Registration("Donor", license, email), await Token(client))).StatusCode);

        await Problem(await Post(client, "/api/auth/register", Registration("Beneficiary", email: email.ToUpperInvariant()), await Token(client)),
            HttpStatusCode.Conflict, "auth.account_exists");
        await Problem(await Post(client, "/api/auth/register", Registration("Beneficiary", license), await Token(client)),
            HttpStatusCode.Conflict, "organization.license_exists");

        var weak = await Post(client, "/api/auth/register", Registration("Donor", password: "short"), await Token(client));
        var weakBody = JsonNode.Parse(await weak.Content.ReadAsStringAsync())!;
        await Problem(weak, HttpStatusCode.BadRequest, "validation");
        Assert.NotEmpty(weakBody["errors"]!["password"]!.AsArray());

        var missing = await Post(client, "/api/auth/register", new { organizationType = "Donor" }, await Token(client));
        var missingBody = JsonNode.Parse(await missing.Content.ReadAsStringAsync())!;
        await Problem(missing, HttpStatusCode.BadRequest, "validation");
        Assert.NotNull(missingBody["errors"]);
    }

    // ---- Cookie behavior: status codes for /api, redirects for MVC

    [Fact]
    public async Task Api_gets_401_and_403_while_mvc_keeps_its_redirects()
    {
        using var anonymous = Client();
        await Problem(await anonymous.GetAsync("/api/test-only/admin"), HttpStatusCode.Unauthorized, "auth.unauthenticated");
        var mvcLogin = await anonymous.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.Redirect, mvcLogin.StatusCode);
        Assert.StartsWith("https://localhost/Auth/Login?ReturnUrl=", mvcLogin.Headers.Location!.ToString());

        using var donor = Client();
        Assert.Equal(HttpStatusCode.OK, (await Login(donor, await User(AppRoles.Donor, OrganizationType.Donor))).StatusCode);
        await Problem(await donor.GetAsync("/api/test-only/admin"), HttpStatusCode.Forbidden, "auth.forbidden");
        var mvcDenied = await donor.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.Redirect, mvcDenied.StatusCode);
        Assert.StartsWith("https://localhost/Admin/AccessDenied", mvcDenied.Headers.Location!.ToString());

        using var admin = Client();
        Assert.Equal(HttpStatusCode.OK, (await Login(admin, await User(AppRoles.Admin))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.GetAsync("/api/test-only/admin")).StatusCode);
    }

    // ---- ProblemDetails

    [Fact]
    public async Task Api_failures_are_sanitized_problem_details()
    {
        using var client = Client();
        var boom = await client.GetAsync("/api/test-only/boom"); // Development host: still no developer page for /api.
        await Problem(boom, HttpStatusCode.InternalServerError, "server.error");
        await Problem(await client.GetAsync("/api/does-not-exist"), HttpStatusCode.NotFound, "not_found");
    }

    // ---- MVC login regression: same enumeration-safe rules as the API

    [Fact]
    public async Task Mvc_login_no_longer_reveals_the_account_or_its_status_before_the_password()
    {
        var active = await User(AppRoles.Donor, OrganizationType.Donor);
        var pending = await User(AppRoles.Donor, OrganizationType.Donor, OrganizationStatus.Pending);
        async Task<HttpResponseMessage> MvcLogin(string email, string password)
        {
            using var client = Client();
            var form = await client.GetStringAsync("/Auth/Login");
            var token = WebUtility.HtmlDecode(Regex.Match(form, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
            return await client.PostAsync("/Auth/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["email"] = email, ["password"] = password, ["__RequestVerificationToken"] = token }));
        }
        async Task<string> Error(string email, string password)
        {
            var response = await MvcLogin(email, password);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("موافقة", html);
            return html.Contains(AuthController.AccountUnavailableMessage) ? "unavailable"
                : html.Contains(AuthController.InvalidCredentialsMessage) ? "invalid" : "none";
        }

        Assert.Equal("invalid", await Error("nobody-" + Guid.NewGuid() + "@r62.local", Password));
        Assert.Equal("invalid", await Error(active, "Wrong-Password!1"));
        Assert.Equal("invalid", await Error(pending, "Wrong-Password!1"));
        Assert.Equal("unavailable", await Error(pending, Password));
        Assert.Equal(HttpStatusCode.Redirect, (await MvcLogin(active, Password)).StatusCode);
    }
}
