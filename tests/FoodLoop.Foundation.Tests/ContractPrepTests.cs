using System.Data.SqlTypes;
using System.Net;
using System.Text.RegularExpressions;
using FoodLoop.Application.Courier;
using FoodLoop.Application.Donations;
using FoodLoop.Application.Identity;
using FoodLoop.Application.Interfaces.Identity;
using FoodLoop.Application.Organizations;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Auditing;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FoodLoop.Foundation.Tests;

// R6.1: typed service outcomes and extracted rules that MVC and the future API share.
public sealed class ContractPrepTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    // Test-only password; never used for a real account.
    private const string Password = "Test-Only!48269";

    private sealed record Actor(Guid Id, string Role, Guid? Org = null) : ICurrentUserService
    {
        public Guid? UserId => Id;
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => Role == role;
        public Task<Guid?> GetOrganizationIdAsync(CancellationToken ct = default) => Task.FromResult(Org);
    }

    private sealed class Directory(Guid courier) : ICourierDirectory
    {
        public Task<bool> IsCourierAsync(Guid id) => Task.FromResult(id == courier);
        public Task<IReadOnlyList<CourierOption>> ListAsync() => Task.FromResult<IReadOnlyList<CourierOption>>([new(courier, "Courier")]);
    }

    private sealed record Data(Organization Donor, Organization Beneficiary, FoodCategory Category, FoodDonation Draft, DonationClaim Claim,
        Actor Admin, Actor Courier, Actor DonorUser, Actor BeneficiaryUser, Actor ForeignDonor);

    private static Organization Org(OrganizationType type, OrganizationStatus status = OrganizationStatus.Active) =>
        new() { Name = "R61 " + type, LicenseNumber = Guid.NewGuid().ToString("N"), Type = type, Status = status };

    private static async Task<Data> Seed(ApplicationDbContext db)
    {
        var donor = Org(OrganizationType.Donor); var foreign = Org(OrganizationType.Donor); var beneficiary = Org(OrganizationType.Beneficiary);
        var category = new FoodCategory { Name = Guid.NewGuid().ToString() };
        FoodDonation Donation(DonationStatus status) => new()
        {
            DonorOrganization = donor, FoodCategory = category, Title = "R61 meals", Quantity = 5, Unit = QuantityUnit.Meals,
            PreparedAtUtc = DateTimeOffset.UtcNow.AddHours(-1), ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2), Status = status, PickupAddress = "Address"
        };
        var draft = Donation(DonationStatus.Draft);
        var claim = new DonationClaim { FoodDonation = Donation(DonationStatus.Claimed), BeneficiaryOrganization = beneficiary };
        Actor User(string role, Organization? org = null)
        {
            var id = Guid.NewGuid();
            db.Users.Add(new ApplicationUser { Id = id, UserName = id.ToString(), NormalizedUserName = id.ToString(), Email = id + "@r61.local",
                NormalizedEmail = id + "@R61.LOCAL", DisplayName = role, Organization = org });
            return new(id, role, org?.Id);
        }
        var data = new Data(donor, beneficiary, category, draft, claim, User(AppRoles.Admin), User(AppRoles.Courier),
            User(AppRoles.Donor, donor), User(AppRoles.Beneficiary, beneficiary), User(AppRoles.Donor, foreign));
        db.AddRange(draft, claim);
        await db.SaveChangesAsync();
        return data;
    }

    private static DonationService Donations(ApplicationDbContext db, Actor user) =>
        new(user, new FoodDonationRepository(db, TimeProvider.System), new FoodCategoryRepository(db), new Repository<Organization>(db),
            new UnitOfWork(db), new AuditService(db, user, TimeProvider.System), TimeProvider.System);

    private static CourierService Courier(ApplicationDbContext db, Actor user, Data d) =>
        new(user, new CourierRepository(db), new Directory(d.Courier.Id), new Repository<Organization>(db), new UnitOfWork(db),
            new AuditService(db, user, TimeProvider.System), TimeProvider.System);

    private static OrganizationApprovalService Approvals(ApplicationDbContext db, Actor admin) =>
        new(admin, new Repository<Organization>(db), new AuditService(db, admin, TimeProvider.System), new UnitOfWork(db));

    private static OrganizationManagementService Management(ApplicationDbContext db, Actor admin) =>
        new(admin, new Repository<Organization>(db), new OrganizationAdminReadRepository(db), new AuditService(db, admin, TimeProvider.System), new UnitOfWork(db));

    private static UpdateDonationRequest Update(FoodDonation d, string title, string rowVersion) =>
        new(d.FoodCategoryId, title, "", 5, QuantityUnit.Meals, d.PreparedAtUtc, d.ExpiresAtUtc, "", "Address", rowVersion);

    // ---- Donation typed outcomes

    [Fact]
    public async Task Donation_failures_carry_a_typed_kind_and_keep_their_messages()
    {
        await using var db = fixture.CreateContext(); var d = await Seed(db);
        var service = Donations(db, d.DonorUser);

        var invalid = await service.CreateAsync(new(d.Category.Id, "  ", "", 5, QuantityUnit.Meals, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), "", "Address"));
        Assert.Equal((DonationFailureKind.Validation, "Title is required."), (invalid.Failure!.Value, invalid.Error));

        var badCategory = await service.CreateAsync(new(Guid.NewGuid(), "Meals", "", 5, QuantityUnit.Meals, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), "", "Address"));
        Assert.Equal(DonationFailureKind.Validation, badCategory.Failure);

        var foreign = await Donations(db, d.ForeignDonor).PublishAsync(d.Draft.Id);
        Assert.Equal((DonationFailureKind.Forbidden, "You cannot publish another organization's donation."), (foreign.Failure!.Value, foreign.Error));
        var foreignEdit = await Donations(db, d.ForeignDonor).UpdateAsync(d.Draft.Id, Update(d.Draft, "X", Convert.ToBase64String(d.Draft.RowVersion)));
        Assert.Equal(DonationFailureKind.Forbidden, foreignEdit.Failure);

        Assert.Equal(DonationFailureKind.NotFound, (await service.PublishAsync(Guid.NewGuid())).Failure);
        Assert.Equal(DonationFailureKind.NotFound, (await service.UpdateAsync(Guid.NewGuid(), Update(d.Draft, "X", "AA=="))).Failure);

        var stale = await service.UpdateAsync(d.Draft.Id, Update(d.Draft, "Stale", Convert.ToBase64String(new byte[8])));
        Assert.Equal((DonationFailureKind.Conflict, "This donation changed. Refresh and try again."), (stale.Failure!.Value, stale.Error));
        Assert.Equal(DonationFailureKind.Conflict, (await service.UpdateAsync(d.Draft.Id, Update(d.Draft, "Bad", "not base64!"))).Failure);

        var published = await service.PublishAsync(d.Draft.Id);
        Assert.True(published.Succeeded);
        Assert.Null(published.Failure);
        var again = await service.PublishAsync(d.Draft.Id);
        Assert.Equal((DonationFailureKind.InvalidState, "Only draft donations can be published."), (again.Failure!.Value, again.Error));
    }

    [Fact]
    public async Task Donation_actions_by_ineligible_accounts_are_forbidden()
    {
        await using var db = fixture.CreateContext(); var d = await Seed(db);
        d.Donor.Status = OrganizationStatus.Suspended; await db.SaveChangesAsync();
        var request = new CreateDonationRequest(d.Category.Id, "Meals", "", 5, QuantityUnit.Meals, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), "", "Address");
        Assert.Equal(DonationFailureKind.Forbidden, (await Donations(db, d.DonorUser).CreateAsync(request)).Failure);
        Assert.Equal(DonationFailureKind.Forbidden, (await Donations(db, d.DonorUser).PublishAsync(d.Draft.Id)).Failure);
        Assert.Equal(DonationFailureKind.Forbidden, (await Donations(db, d.BeneficiaryUser).CreateAsync(request)).Failure);
        Assert.Equal(DonationStatus.Draft, d.Draft.Status);
    }

    // ---- Courier typed outcomes, canIssue and next step

    [Fact]
    public async Task Courier_failures_carry_a_typed_kind()
    {
        await using var db = fixture.CreateContext(); var d = await Seed(db);
        Assert.Equal(CourierFailureKind.NotFound, (await Courier(db, d.Admin, d).AssignAsync(Guid.NewGuid(), d.Courier.Id, default)).Failure);
        Assert.Equal(CourierFailureKind.Validation, (await Courier(db, d.Admin, d).AssignAsync(d.Claim.Id, Guid.NewGuid(), default)).Failure);
        Assert.Equal(CourierFailureKind.InvalidState, (await Courier(db, d.DonorUser, d).IssueAsync(d.Claim.Id, HandoverType.Pickup, default)).Failure);

        Assert.True((await Courier(db, d.Admin, d).AssignAsync(d.Claim.Id, d.Courier.Id, default)).Succeeded);
        var courier = Courier(db, d.Courier, d);
        Assert.Equal(CourierFailureKind.Validation, (await courier.VerifyAsync(d.Claim.Id, "short", HandoverType.Pickup, default)).Failure);
        Assert.Equal(CourierFailureKind.Validation, (await courier.VerifyAsync(d.Claim.Id, new string('0', 64), HandoverType.Pickup, default)).Failure);
        Assert.Equal(CourierFailureKind.Validation, (await courier.VerifyAsync(d.Claim.Id, new string('0', 64), (HandoverType)9, default)).Failure);
        Assert.Equal(CourierFailureKind.InvalidState, (await courier.VerifyAsync(d.Claim.Id, new string('0', 64), HandoverType.Delivery, default)).Failure);
        // Authorization keeps its existing mechanism.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Courier(db, d.Courier with { Id = Guid.NewGuid() }, d).VerifyAsync(d.Claim.Id, "x", HandoverType.Pickup, default));
    }

    [Fact]
    public async Task CanIssue_follows_the_server_handover_rule_for_each_side()
    {
        await using var db = fixture.CreateContext(); var d = await Seed(db);
        async Task<HandoverTaskItem> Item(Actor actor) => Assert.Single(await Courier(db, actor, d).OrganizationTasksAsync(default), x => x.ClaimId == d.Claim.Id);

        // Booked, no courier yet: nobody can issue.
        var donorItem = await Item(d.DonorUser); var beneficiaryItem = await Item(d.BeneficiaryUser);
        Assert.Equal((HandoverType.Pickup, false), (donorItem.IssueType, donorItem.CanIssue));
        Assert.Equal((HandoverType.Delivery, false), (beneficiaryItem.IssueType, beneficiaryItem.CanIssue));

        Assert.True((await Courier(db, d.Admin, d).AssignAsync(d.Claim.Id, d.Courier.Id, default)).Succeeded);
        Assert.True((await Item(d.DonorUser)).CanIssue);
        Assert.False((await Item(d.BeneficiaryUser)).CanIssue);

        var pickup = await Courier(db, d.DonorUser, d).IssueAsync(d.Claim.Id, HandoverType.Pickup, default);
        Assert.True((await Courier(db, d.Courier, d).VerifyAsync(d.Claim.Id, pickup.Token, HandoverType.Pickup, default)).Succeeded);
        Assert.False((await Item(d.DonorUser)).CanIssue);
        Assert.True((await Item(d.BeneficiaryUser)).CanIssue);

        // Counterpart organization no longer active: listing and IssueAsync agree.
        d.Donor.Status = OrganizationStatus.Suspended; await db.SaveChangesAsync();
        Assert.False((await Item(d.BeneficiaryUser)).CanIssue);
        Assert.Equal(CourierFailureKind.InvalidState, (await Courier(db, d.BeneficiaryUser, d).IssueAsync(d.Claim.Id, HandoverType.Delivery, default)).Failure);

        d.Donor.Status = OrganizationStatus.Active; d.Claim.FoodDonation.ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1); await db.SaveChangesAsync();
        Assert.False((await Item(d.BeneficiaryUser)).CanIssue);
    }

    [Theory]
    [InlineData(ClaimStatus.PickupPending, CourierNextStep.VerifyPickup)]
    [InlineData(ClaimStatus.InTransit, CourierNextStep.VerifyDelivery)]
    [InlineData(ClaimStatus.Closed, CourierNextStep.Completed)]
    [InlineData(ClaimStatus.Booked, CourierNextStep.None)]
    [InlineData(ClaimStatus.PickedUp, CourierNextStep.None)]
    [InlineData(ClaimStatus.Delivered, CourierNextStep.None)]
    [InlineData(ClaimStatus.Cancelled, CourierNextStep.None)]
    [InlineData(ClaimStatus.Failed, CourierNextStep.None)]
    public void Courier_next_step_maps_every_claim_status(ClaimStatus status, CourierNextStep expected) =>
        Assert.Equal(expected, CourierService.NextStep(status));

    [Fact]
    public async Task My_tasks_returns_a_projection_with_the_next_step()
    {
        await using var db = fixture.CreateContext(); var d = await Seed(db);
        Assert.True((await Courier(db, d.Admin, d).AssignAsync(d.Claim.Id, d.Courier.Id, default)).Succeeded);
        var task = Assert.Single(await Courier(db, d.Courier, d).MyTasksAsync(default));
        Assert.Equal(new CourierTaskItem(d.Claim.Id, "R61 meals", ClaimStatus.PickupPending, CourierNextStep.VerifyPickup), task);
    }

    // ---- Organization approvals and pending query

    [Fact]
    public async Task Organization_decisions_return_typed_outcomes()
    {
        await using var db = fixture.CreateContext(); var d = await Seed(db);
        var approve = Org(OrganizationType.Donor, OrganizationStatus.Pending); var reject = Org(OrganizationType.Beneficiary, OrganizationStatus.Pending);
        db.AddRange(approve, reject); await db.SaveChangesAsync();
        var service = Approvals(db, d.Admin);

        Assert.Equal(OrganizationStatusChangeOutcome.Updated, (await service.DecideAsync(approve.Id, true, default)).Outcome);
        Assert.Equal(OrganizationStatusChangeOutcome.Updated, (await service.DecideAsync(reject.Id, false, default)).Outcome);
        Assert.Equal((OrganizationStatus.Active, OrganizationStatus.Rejected), (approve.Status, reject.Status));

        var missing = await service.DecideAsync(Guid.NewGuid(), true, default);
        Assert.Equal((OrganizationStatusChangeOutcome.NotFound, "Organization not found."), (missing.Outcome, missing.Error));
        var processed = await service.DecideAsync(approve.Id, false, default);
        Assert.Equal((OrganizationStatusChangeOutcome.InvalidState, "This request has already been processed."), (processed.Outcome, processed.Error));
        Assert.Equal(OrganizationStatus.Active, approve.Status);
    }

    [Fact]
    public async Task Pending_query_returns_only_pending_as_a_safe_ordered_projection()
    {
        await using var db = fixture.CreateContext(); var d = await Seed(db);
        var at = DateTimeOffset.UtcNow.AddDays(-3);
        var older = Org(OrganizationType.Donor, OrganizationStatus.Pending); older.CreatedAtUtc = at.AddMinutes(-5);
        var tieA = Org(OrganizationType.Beneficiary, OrganizationStatus.Pending); tieA.CreatedAtUtc = at;
        var tieB = Org(OrganizationType.Donor, OrganizationStatus.Pending); tieB.CreatedAtUtc = at;
        var rejected = Org(OrganizationType.Donor, OrganizationStatus.Rejected); rejected.CreatedAtUtc = at;
        db.AddRange(older, tieA, tieB, rejected); await db.SaveChangesAsync();

        var pending = await Management(db, d.Admin).GetPendingAsync();
        var pendingIds = await db.Organizations.Where(x => x.Status == OrganizationStatus.Pending).Select(x => x.Id).ToListAsync();
        Assert.Equal(pendingIds.Order(), pending.Select(x => x.Id).Order());
        Assert.DoesNotContain(pending, x => x.Id == rejected.Id || x.Id == d.Donor.Id);

        var ties = new[] { tieA, tieB }.OrderBy(x => new SqlGuid(x.Id)).Select(x => x.Id);
        Assert.Equal(new[] { older.Id }.Concat(ties), pending.Where(x => x.Id == older.Id || x.Id == tieA.Id || x.Id == tieB.Id).Select(x => x.Id));
        Assert.Equal(new PendingOrganizationItem(older.Id, older.Name, older.Type, older.LicenseNumber, older.CreatedAtUtc), pending.Single(x => x.Id == older.Id));
        Assert.Equal(new[] { "Id", "Name", "Type", "LicenseNumber", "CreatedAtUtc" }, typeof(PendingOrganizationItem).GetProperties().Select(x => x.Name));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Management(db, d.DonorUser).GetPendingAsync());
    }

    // ---- Login eligibility

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(OrganizationType.Donor, OrganizationStatus.Active, true)]
    [InlineData(OrganizationType.Beneficiary, OrganizationStatus.Active, true)]
    [InlineData(OrganizationType.Donor, OrganizationStatus.Pending, false)]
    [InlineData(OrganizationType.Beneficiary, OrganizationStatus.Pending, false)]
    [InlineData(OrganizationType.Donor, OrganizationStatus.Rejected, false)]
    [InlineData(OrganizationType.Beneficiary, OrganizationStatus.Rejected, false)]
    [InlineData(OrganizationType.Donor, OrganizationStatus.Suspended, false)]
    [InlineData(OrganizationType.Beneficiary, OrganizationStatus.Suspended, true)]
    public void Login_eligibility_matches_the_organization_status_rule(OrganizationType? type, OrganizationStatus? status, bool expected) =>
        Assert.Equal(expected, LoginEligibility.CanSignIn(type, status));

    private WebApplicationFactory<Program> Host(Action<IWebHostBuilder>? configure = null) => new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
    {
        b.UseEnvironment("Development").UseSetting("ConnectionStrings:DefaultConnection", fixture.ConnectionString);
        configure?.Invoke(b);
    });

    private static async Task EnsureRoles(WebApplicationFactory<Program> host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { AppRoles.Donor, AppRoles.Beneficiary })
            if (!await roles.RoleExistsAsync(role)) Assert.True((await roles.CreateAsync(new(role))).Succeeded);
    }

    // Synchronous on purpose: the accessor is AsyncLocal, so it must be set in the caller's context.
    private static AccountService Accounts(AsyncServiceScope scope)
    {
        // SignInManager needs a request to write the auth cookie to.
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        return scope.ServiceProvider.GetRequiredService<AccountService>();
    }

    private static RegisterOrganizationRequest Registration(OrganizationType type, string? license = null, string? email = null) =>
        new("R61 registration", license ?? Guid.NewGuid().ToString("N"), type, email ?? Guid.NewGuid() + "@r61.local");

    [Fact]
    public async Task Sign_in_outcomes_follow_the_extracted_rule()
    {
        using var host = Host();
        await EnsureRoles(host);
        var request = Registration(OrganizationType.Donor);
        await using (var scope = host.Services.CreateAsyncScope())
            Assert.True((await Accounts(scope).RegisterAsync(request, Password)).Succeeded);

        async Task<LoginOutcome> SignIn(string? email, string? password)
        {
            await using var scope = host.Services.CreateAsyncScope();
            return (await Accounts(scope).SignInAsync(email, password)).Outcome;
        }
        async Task SetStatus(OrganizationStatus status)
        {
            await using var db = fixture.CreateContext();
            (await db.Organizations.SingleAsync(x => x.LicenseNumber == request.LicenseNumber)).Status = status;
            await db.SaveChangesAsync();
        }

        // R6.2: unknown account and wrong password are one outcome, and status is only reported after the password is proven.
        Assert.Equal(LoginOutcome.InvalidCredentials, await SignIn("nobody-" + Guid.NewGuid() + "@r61.local", Password));
        Assert.Equal(LoginOutcome.InvalidCredentials, await SignIn(null, Password));
        Assert.Equal(LoginOutcome.InvalidCredentials, await SignIn(request.Email, "wrong-password-1"));
        Assert.Equal(LoginOutcome.AccountUnavailable, await SignIn(request.Email, Password));
        await SetStatus(OrganizationStatus.Active);
        Assert.Equal(LoginOutcome.InvalidCredentials, await SignIn(request.Email, "wrong-password-1"));
        Assert.Equal(LoginOutcome.Succeeded, await SignIn(request.Email, Password));
        Assert.Equal(LoginOutcome.Succeeded, await SignIn(request.Email!.ToUpperInvariant(), Password));
        await SetStatus(OrganizationStatus.Suspended);
        Assert.Equal(LoginOutcome.AccountUnavailable, await SignIn(request.Email, Password));
    }

    // ---- Registration

    [Theory]
    [InlineData(OrganizationType.Donor, AppRoles.Donor)]
    [InlineData(OrganizationType.Beneficiary, AppRoles.Beneficiary)]
    public async Task Registration_creates_a_pending_organization_with_only_the_matching_role(OrganizationType type, string role)
    {
        using var host = Host();
        await EnsureRoles(host);
        var request = Registration(type);
        await using (var scope = host.Services.CreateAsyncScope())
            Assert.Equal(RegistrationOutcome.Succeeded, (await Accounts(scope).RegisterAsync(request, Password)).Outcome);

        await using var db = fixture.CreateContext();
        var user = await db.Users.Include(x => x.Organization).SingleAsync(x => x.Email == request.Email);
        Assert.Equal((OrganizationStatus.Pending, type, request.LicenseNumber), (user.Organization!.Status, user.Organization.Type, user.Organization.LicenseNumber));
        var assigned = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where ur.UserId == user.Id select r.Name).ToListAsync();
        Assert.Equal(new[] { role }, assigned);
    }

    [Fact]
    public async Task Duplicate_license_is_a_controlled_failure_and_leaves_no_second_account()
    {
        using var host = Host();
        await EnsureRoles(host);
        var first = Registration(OrganizationType.Donor);
        var second = Registration(OrganizationType.Beneficiary, license: first.LicenseNumber);
        await using (var scope = host.Services.CreateAsyncScope())
            Assert.True((await Accounts(scope).RegisterAsync(first, Password)).Succeeded);
        await using (var scope = host.Services.CreateAsyncScope())
            Assert.Equal(RegistrationOutcome.DuplicateLicense, (await Accounts(scope).RegisterAsync(second, Password)).Outcome);

        await using var db = fixture.CreateContext();
        Assert.Equal(1, await db.Organizations.CountAsync(x => x.LicenseNumber == first.LicenseNumber));
        Assert.False(await db.Users.AnyAsync(x => x.Email == second.Email));
    }

    [Fact]
    public async Task Identity_failures_leave_no_orphan_organization()
    {
        using var host = Host();
        await EnsureRoles(host);
        var weak = Registration(OrganizationType.Donor);
        var taken = Registration(OrganizationType.Donor);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var accounts = Accounts(scope);
            var result = await accounts.RegisterAsync(weak, "short");
            Assert.Equal(RegistrationOutcome.IdentityFailed, result.Outcome);
            Assert.NotEmpty(result.Errors);
            Assert.True((await accounts.RegisterAsync(taken, Password)).Succeeded);
        }
        var sameEmail = Registration(OrganizationType.Beneficiary, email: taken.Email);
        await using (var scope = host.Services.CreateAsyncScope())
            Assert.Equal(RegistrationOutcome.DuplicateAccount, (await Accounts(scope).RegisterAsync(sameEmail, Password)).Outcome);
        await using (var scope = host.Services.CreateAsyncScope())
            Assert.Equal(RegistrationOutcome.InvalidInput, (await Accounts(scope).RegisterAsync(Registration(OrganizationType.Donor) with { OrganizationName = " " }, Password)).Outcome);

        await using var db = fixture.CreateContext();
        Assert.False(await db.Organizations.AnyAsync(x => x.LicenseNumber == weak.LicenseNumber || x.LicenseNumber == sameEmail.LicenseNumber));
        Assert.False(await db.Users.AnyAsync(x => x.Email == weak.Email));
    }

    [Fact]
    public async Task Mvc_register_shows_duplicate_license_instead_of_failing()
    {
        using var host = Host();
        await EnsureRoles(host);
        using var client = host.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var license = Guid.NewGuid().ToString("N");
        async Task<HttpResponseMessage> Register()
        {
            var form = await client.GetStringAsync("/Auth/Register");
            return await client.PostAsync("/Auth/Register", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["orgName"] = "R61 MVC", ["licenseNumber"] = license, ["orgType"] = "Donor", ["email"] = Guid.NewGuid() + "@r61.local",
                ["password"] = Password, ["__RequestVerificationToken"] = AntiForgery(form)
            }));
        }
        Assert.Equal(HttpStatusCode.Redirect, (await Register()).StatusCode);
        var duplicate = await Register();
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        var html = await duplicate.Content.ReadAsStringAsync();
        Assert.Contains("An organization with this license number is already registered.", html);
        Assert.DoesNotContain("IX_Organizations", html);
    }

    // ---- MVC regression over the reworked routes, with header-based test authentication.

    private sealed class HeaderHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Test-User"], out var id)) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new System.Security.Claims.ClaimsIdentity([
                new(System.Security.Claims.ClaimTypes.NameIdentifier, id.ToString()),
                new(System.Security.Claims.ClaimTypes.Role, Request.Headers["X-Test-Role"].ToString())], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    private static string AntiForgery(string html) =>
        WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);

    [Fact]
    public async Task Mvc_pages_render_from_the_new_projections()
    {
        await using var db = fixture.CreateContext(); var d = await Seed(db);
        var pending = Org(OrganizationType.Beneficiary, OrganizationStatus.Pending); pending.Name = "R61 pending " + Guid.NewGuid().ToString("N")[..6];
        db.Add(pending); await db.SaveChangesAsync();
        using var host = Host(b => b.ConfigureTestServices(s => s.AddAuthentication(o => o.DefaultAuthenticateScheme = "R61")
            .AddScheme<AuthenticationSchemeOptions, HeaderHandler>("R61", _ => { })));
        HttpClient As(Actor actor)
        {
            var client = host.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
            client.DefaultRequestHeaders.Add("X-Test-User", actor.Id.ToString());
            client.DefaultRequestHeaders.Add("X-Test-Role", actor.Role);
            return client;
        }
        using var admin = As(d.Admin); using var donor = As(d.DonorUser); using var courier = As(d.Courier);

        var pendingHtml = await admin.GetStringAsync("/Organizations/PendingRequests");
        Assert.Contains(pending.Name, pendingHtml);
        var rejected = await admin.PostAsync("/Organizations/RejectOrganization", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["id"] = pending.Id.ToString(), ["__RequestVerificationToken"] = AntiForgery(pendingHtml) }));
        Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
        await db.Entry(pending).ReloadAsync(); Assert.Equal(OrganizationStatus.Rejected, pending.Status);

        Assert.DoesNotContain("Generate handover code", await donor.GetStringAsync("/Courier/Codes"));
        Assert.True((await Courier(db, d.Admin, d).AssignAsync(d.Claim.Id, d.Courier.Id, default)).Succeeded);
        Assert.Contains("Generate handover code", await donor.GetStringAsync("/Courier/Codes"));
        var tasks = await courier.GetStringAsync("/Courier/MyTasks");
        Assert.Contains("Awaiting pickup verification", tasks);
        Assert.Contains("/Courier/VerifyHandover?claimId=" + d.Claim.Id, tasks);
        Assert.Contains("Scan Donor QR Code to complete Pickup", await courier.GetStringAsync("/Courier/Details/" + d.Claim.Id));

        var edit = await donor.GetStringAsync("/Donations/Edit/" + d.Draft.Id);
        var rowVersion = WebUtility.HtmlDecode(Regex.Match(edit, "name=\"RowVersion\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Dictionary<string, string> Fields(string title, string version) => new()
        {
            ["Id"] = d.Draft.Id.ToString(), ["FoodCategoryId"] = d.Category.Id.ToString(), ["Title"] = title, ["Description"] = "Fresh",
            ["Quantity"] = "5", ["Unit"] = "Meals", ["PreparedAt"] = d.Draft.PreparedAtUtc.ToString("o"), ["ExpiresAt"] = d.Draft.ExpiresAtUtc.ToString("o"),
            ["StorageInstructions"] = "Chilled", ["PickupAddress"] = "Address", ["RowVersion"] = version, ["__RequestVerificationToken"] = AntiForgery(edit)
        };
        var stale = await donor.PostAsync("/Donations/Edit/" + d.Draft.Id, new FormUrlEncodedContent(Fields("Stale", Convert.ToBase64String(new byte[8]))));
        Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        Assert.Contains("This donation changed. Refresh and try again.", await stale.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await donor.PostAsync("/Donations/Edit/" + d.Draft.Id, new FormUrlEncodedContent(Fields("Edited", rowVersion)))).StatusCode);

        var logout = await donor.PostAsync("/Auth/Logout", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = AntiForgery(edit) }));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal("/Auth/Login", logout.Headers.Location!.OriginalString);
    }
}

// Own database with no roles, so registration fails after Identity has already written the user and organization.
public sealed class RegistrationRollbackTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task Missing_roles_roll_back_the_created_user_and_organization()
    {
        using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.UseEnvironment("Development").UseSetting("ConnectionStrings:DefaultConnection", fixture.ConnectionString));
        var request = new RegisterOrganizationRequest("R61 rollback", Guid.NewGuid().ToString("N"), OrganizationType.Donor, Guid.NewGuid() + "@r61.local");
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<AccountService>().RegisterAsync(request, "Test-Only!48269");
            Assert.Equal(RegistrationOutcome.RolesNotConfigured, result.Outcome);
        }
        await using var db = fixture.CreateContext();
        Assert.False(await db.Organizations.AnyAsync(x => x.LicenseNumber == request.LicenseNumber));
        Assert.False(await db.Users.AnyAsync(x => x.Email == request.Email));
    }
}
