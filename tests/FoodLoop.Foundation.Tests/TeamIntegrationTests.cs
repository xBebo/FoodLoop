using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using FoodLoop.Application.Claims;
using FoodLoop.Application.Courier;
using FoodLoop.Application.Donations;
using FoodLoop.Application.Organizations;
using FoodLoop.Application.Interfaces.Identity;
using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Auditing;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
namespace FoodLoop.Foundation.Tests;

public sealed class TeamIntegrationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
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
    private record Data(Organization Donor, Organization Beneficiary, FoodCategory Category, DonationClaim Claim, Actor Admin, Actor Courier, Actor DonorUser, Actor BeneficiaryUser);
    private async Task<Data> Seed(ApplicationDbContext db)
    {
        Organization Org(OrganizationType type) => new() { Name = "Test", LicenseNumber = Guid.NewGuid().ToString(), Type = type, Status = OrganizationStatus.Active };
        var donor = Org(OrganizationType.Donor); var beneficiary = Org(OrganizationType.Beneficiary);
        var category = new FoodCategory { Name = Guid.NewGuid().ToString() };
        var donation = new FoodDonation { DonorOrganization = donor, FoodCategory = category, Title = "Meals", Quantity = 5, Unit = QuantityUnit.Meals, PreparedAtUtc = DateTimeOffset.UtcNow.AddHours(-1), ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2), Status = DonationStatus.Claimed };
        var claim = new DonationClaim { FoodDonation = donation, BeneficiaryOrganization = beneficiary };
        Actor AddUser(string role, Organization? org = null)
        {
            var id = Guid.NewGuid();
            db.Users.Add(new ApplicationUser { Id = id, UserName = id.ToString(), NormalizedUserName = id.ToString(), Email = id + "@test.local", NormalizedEmail = id + "@TEST.LOCAL", DisplayName = role, Organization = org });
            return new(id, role, org?.Id);
        }
        var data = new Data(donor, beneficiary, category, claim, AddUser("Admin"), AddUser("Courier"), AddUser("Donor", donor), AddUser("Beneficiary", beneficiary));
        db.Add(claim); await db.SaveChangesAsync(); return data;
    }
    private CourierService Service(ApplicationDbContext db, Actor user, Data d, IUnitOfWork? uow = null) =>
        new(user, new CourierRepository(db), new Directory(d.Courier.Id), new Repository<Organization>(db), uow ?? new UnitOfWork(db), new AuditService(db, user, TimeProvider.System), TimeProvider.System);
    private async Task<string> PreparePickup(ApplicationDbContext db, Data d)
    {
        Assert.True((await Service(db, d.Admin, d).AssignAsync(d.Claim.Id, d.Courier.Id, default)).Succeeded);
        var code = await Service(db, d.DonorUser, d).IssueAsync(d.Claim.Id, HandoverType.Pickup, default);
        Assert.True(code.Succeeded, code.Error); return code.Token!;
    }
    [Fact]
    public async Task Publish_claim_pickup_delivery_updates_dashboard_and_audit_atomically()
    {
        await using var db = fixture.CreateContext(); var d = await Seed(db);
        var before = await new AdminReadRepository(db).GetDashboardAsync(DateTimeOffset.UtcNow);
        var donations = new FoodDonationRepository(db, TimeProvider.System);
        var service = new DonationService(d.DonorUser, donations, new FoodCategoryRepository(db), new Repository<Organization>(db), new UnitOfWork(db), new AuditService(db, d.DonorUser, TimeProvider.System), TimeProvider.System);
        var created = await service.CreateAsync(new(d.Category.Id, "New donation", "", 5, QuantityUnit.Meals, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1), "", "Address"));
        Assert.True(created.Succeeded);
        Assert.True((await service.PublishAsync(created.DonationId!.Value)).Succeeded);
        var claims = new ClaimService(d.BeneficiaryUser, donations, new Repository<Organization>(db), new ClaimRepository(db), new AuditService(db, d.BeneficiaryUser, TimeProvider.System), new UnitOfWork(db), TimeProvider.System);
        var booked = await claims.CreateAsync(created.DonationId.Value, default);
        var claim = await db.DonationClaims.SingleAsync(x => x.FoodDonationId == created.DonationId.Value);
        d = d with { Claim = claim };
        var pickup = await PreparePickup(db, d);
        Assert.True((await Service(db, d.Courier, d).VerifyAsync(claim.Id, pickup, HandoverType.Pickup, default)).Succeeded);
        var delivery = await Service(db, d.BeneficiaryUser, d).IssueAsync(claim.Id, HandoverType.Delivery, default);
        Assert.True(delivery.Succeeded);
        Assert.True((await Service(db, d.Courier, d).VerifyAsync(claim.Id, delivery.Token, HandoverType.Delivery, default)).Succeeded);
        Assert.Equal(ClaimStatus.Closed, claim.Status); Assert.Equal(DonationStatus.Closed, claim.FoodDonation.Status);
        Assert.Equal(2, await db.HandoverRecords.CountAsync(x => x.DonationClaimId == claim.Id));
        Assert.Equal(2, await db.QrVerificationTokens.CountAsync(x => x.DonationClaimId == claim.Id && x.UsedAtUtc != null));
        Assert.Equal(before.ClosedDeliveries + 1, (await new AdminReadRepository(db).GetDashboardAsync(DateTimeOffset.UtcNow)).ClosedDeliveries);
        Assert.Equal(d.Courier.Id, (await db.AuditLogs.SingleAsync(x => x.EntityId == claim.Id && x.Action == "DeliveryVerified")).ActorUserId);
    }
    [Fact]
    public async Task Wrong_code_and_delivery_before_pickup_leave_no_evidence()
    {
        await using var db = fixture.CreateContext(); var d = await Seed(db); var token = await PreparePickup(db, d);
        Assert.False((await Service(db, d.Courier, d).VerifyAsync(d.Claim.Id, new string('0',64), HandoverType.Pickup, default)).Succeeded);
        Assert.False((await Service(db, d.Courier, d).VerifyAsync(d.Claim.Id, token, HandoverType.Delivery, default)).Succeeded);
        Assert.Empty(await db.HandoverRecords.Where(x => x.DonationClaimId == d.Claim.Id).ToListAsync());
        Assert.Equal(ClaimStatus.PickupPending, d.Claim.Status);
    }
    [Fact]
    public async Task Code_regeneration_revokes_old_code_and_replay_is_rejected()
    {
        await using var db = fixture.CreateContext(); var d = await Seed(db); var old = await PreparePickup(db, d);
        var fresh = await Service(db, d.DonorUser, d).IssueAsync(d.Claim.Id, HandoverType.Pickup, default);
        Assert.False((await Service(db, d.Courier, d).VerifyAsync(d.Claim.Id, old, HandoverType.Pickup, default)).Succeeded);
        Assert.True((await Service(db, d.Courier, d).VerifyAsync(d.Claim.Id, fresh.Token, HandoverType.Pickup, default)).Succeeded);
        Assert.False((await Service(db, d.Courier, d).VerifyAsync(d.Claim.Id, fresh.Token, HandoverType.Pickup, default)).Succeeded);
    }
    [Fact]
    public async Task Wrong_courier_and_wrong_organization_are_forbidden()
    {
        await using var db = fixture.CreateContext(); var d = await Seed(db); var token = await PreparePickup(db,d);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(db, d.Courier with { Id = Guid.NewGuid() }, d).VerifyAsync(d.Claim.Id, token, HandoverType.Pickup, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(db, d.DonorUser with { Org = Guid.NewGuid() }, d).IssueAsync(d.Claim.Id, HandoverType.Pickup, default));
        Assert.Empty(await Service(db, d.Courier with { Id = Guid.NewGuid() }, d).MyTasksAsync(default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(db, d.DonorUser, d).AssignAsync(d.Claim.Id, d.Courier.Id, default));
    }
    [Fact]
    public async Task Suspended_beneficiary_blocks_delivery_with_previously_issued_code()
    {
        await using var db = fixture.CreateContext(); var d = await Seed(db); var token = await PreparePickup(db,d);
        Assert.True((await Service(db,d.Courier,d).VerifyAsync(d.Claim.Id,token,HandoverType.Pickup,default)).Succeeded);
        var delivery = await Service(db,d.BeneficiaryUser,d).IssueAsync(d.Claim.Id,HandoverType.Delivery,default);
        d.Beneficiary.Status = OrganizationStatus.Suspended; await db.SaveChangesAsync();
        Assert.False((await Service(db,d.Courier,d).VerifyAsync(d.Claim.Id,delivery.Token,HandoverType.Delivery,default)).Succeeded);
        Assert.Equal(ClaimStatus.InTransit,d.Claim.Status);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Expired_code_or_donation_is_rejected(bool expireCode)
    {
        await using var db = fixture.CreateContext(); var d = await Seed(db); var token = await PreparePickup(db,d);
        if (expireCode) { var stored = await db.QrVerificationTokens.SingleAsync(x=>x.DonationClaimId==d.Claim.Id); stored.CreatedAtUtc=DateTimeOffset.UtcNow.AddHours(-2); stored.ExpiresAtUtc=DateTimeOffset.UtcNow.AddHours(-1); }
        else d.Claim.FoodDonation.ExpiresAtUtc=DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        Assert.False((await Service(db,d.Courier,d).VerifyAsync(d.Claim.Id,token,HandoverType.Pickup,default)).Succeeded);
    }
    [Fact]
    public async Task Invalid_courier_or_assignment_after_pickup_is_rejected()
    {
        await using var db = fixture.CreateContext(); var d = await Seed(db);
        Assert.False((await Service(db,d.Admin,d).AssignAsync(d.Claim.Id,Guid.NewGuid(),default)).Succeeded);
        var code = await PreparePickup(db,d);
        Assert.True((await Service(db,d.Courier,d).VerifyAsync(d.Claim.Id,code,HandoverType.Pickup,default)).Succeeded);
        Assert.False((await Service(db,d.Admin,d).AssignAsync(d.Claim.Id,d.Courier.Id,default)).Succeeded);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Organization_decision_records_actor_once(bool approve)
    {
        await using var db = fixture.CreateContext(); var d = await Seed(db);
        d.Donor.Status=OrganizationStatus.Pending; await db.SaveChangesAsync();
        var service = new OrganizationApprovalService(d.Admin,new Repository<Organization>(db),new AuditService(db,d.Admin,TimeProvider.System),new UnitOfWork(db));
        Assert.Null(await service.DecideAsync(d.Donor.Id,approve,default));
        Assert.NotNull(await service.DecideAsync(d.Donor.Id,!approve,default));
        var audit = await db.AuditLogs.SingleAsync(x=>x.EntityId==d.Donor.Id);
        Assert.Equal(d.Admin.Id,audit.ActorUserId);
        Assert.Equal(approve?OrganizationStatus.Active:OrganizationStatus.Rejected,d.Donor.Status);
    }
    [Fact]
    public async Task Marketplace_extreme_page_is_empty()
    {
        await using var db = fixture.CreateContext();
        Assert.Empty(await new FoodDonationRepository(db,TimeProvider.System).GetAvailableAsync(int.MaxValue,100));
    }
    private sealed class Gate
    {
        private int count;
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Wait() { if(Interlocked.Increment(ref count)==2) ready.TrySetResult(); return ready.Task.WaitAsync(TimeSpan.FromSeconds(20)); }
    }
    private sealed class GatedSave(ApplicationDbContext db, Gate gate) : IUnitOfWork
    {
        public async Task<int> SaveChangesAsync(CancellationToken ct=default) { await gate.Wait(); return await new UnitOfWork(db).SaveChangesAsync(ct); }
        public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken ct=default) => new UnitOfWork(db).BeginTransactionAsync(ct);
    }
    [Fact]
    public async Task Concurrent_pickup_commits_exactly_one_handover_and_audit()
    {
        await using var setup=fixture.CreateContext(); var d=await Seed(setup); var code=await PreparePickup(setup,d);
        await using var a=fixture.CreateContext(); await using var b=fixture.CreateContext(); var gate=new Gate();
        var results=await Task.WhenAll(
            Service(a,d.Courier,d,new GatedSave(a,gate)).VerifyAsync(d.Claim.Id,code,HandoverType.Pickup,default),
            Service(b,d.Courier,d,new GatedSave(b,gate)).VerifyAsync(d.Claim.Id,code,HandoverType.Pickup,default));
        Assert.Single(results, x=>x.Succeeded);
        await using var check=fixture.CreateContext();
        Assert.Equal(1,await check.HandoverRecords.CountAsync(x=>x.DonationClaimId==d.Claim.Id));
        Assert.Equal(1,await check.AuditLogs.CountAsync(x=>x.EntityId==d.Claim.Id && x.Action=="PickupVerified"));
    }

    private sealed class WebHost(string connection) : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development").UseSetting("ConnectionStrings:DefaultConnection", connection);
            builder.ConfigureTestServices(services => services.AddAuthentication(o => o.DefaultAuthenticateScheme = "IntegrationTest")
                .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, HeaderHandler>("IntegrationTest", _ => {}));
        }
    }
    private sealed class HeaderHandler(Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder)
        : Microsoft.AspNetCore.Authentication.AuthenticationHandler<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>(options,logger,encoder)
    {
        protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Test-User"], out var id)) return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult());
            var identity = new System.Security.Claims.ClaimsIdentity([
                new(System.Security.Claims.ClaimTypes.NameIdentifier,id.ToString()),
                new(System.Security.Claims.ClaimTypes.Role,Request.Headers["X-Test-Role"].ToString())],Scheme.Name);
            return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.Success(
                new Microsoft.AspNetCore.Authentication.AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(identity),Scheme.Name)));
        }
    }
    private static HttpClient Client(WebHost host, Actor user)
    {
        var client=host.CreateClient(new(){AllowAutoRedirect=false,BaseAddress=new Uri("https://localhost")});
        client.DefaultRequestHeaders.Add("X-Test-User",user.Id.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role",user.Role);
        return client;
    }
    private static string AntiForgery(string html)
    {
        var match=System.Text.RegularExpressions.Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success,"Missing antiforgery input");
        return System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
    }
    [Fact]
    public async Task Approval_form_posts_to_the_audited_controller_and_requires_admin()
    {
        await using var db=fixture.CreateContext(); var d=await Seed(db); d.Donor.Status=OrganizationStatus.Pending; await db.SaveChangesAsync();
        using var host=new WebHost(fixture.ConnectionString); using var admin=Client(host,d.Admin);
        var html=await admin.GetStringAsync("/Organizations/PendingRequests");
        Assert.Contains("/Organizations/ApproveOrganization",html); Assert.DoesNotContain("/Auth/ApproveOrganization",html);
        var result=await admin.PostAsync("/Organizations/ApproveOrganization",new FormUrlEncodedContent(new Dictionary<string,string>{
            ["id"]=d.Donor.Id.ToString(),["__RequestVerificationToken"]=AntiForgery(html)}));
        Assert.Equal(System.Net.HttpStatusCode.Redirect,result.StatusCode);
        await db.Entry(d.Donor).ReloadAsync(); Assert.Equal(OrganizationStatus.Active,d.Donor.Status);
        Assert.Equal(d.Admin.Id,(await db.AuditLogs.SingleAsync(x=>x.EntityId==d.Donor.Id)).ActorUserId);
        using var donor=Client(host,d.DonorUser);
        var forbidden=await donor.GetAsync("/Organizations/PendingRequests");
        Assert.Equal(System.Net.HttpStatusCode.Redirect,forbidden.StatusCode);
        Assert.Contains("/Admin/AccessDenied",forbidden.Headers.Location!.ToString());
        Assert.Equal(System.Net.HttpStatusCode.NotFound,(await admin.GetAsync("/Auth/ApproveOrganization")).StatusCode);
    }
    [Fact]
    public async Task Courier_pages_render_and_form_submits_real_bound_code_with_antiforgery()
    {
        await using var db=fixture.CreateContext(); var d=await Seed(db); await PreparePickup(db,d);
        using var host=new WebHost(fixture.ConnectionString); using var courier=Client(host,d.Courier);
        using var donor=Client(host,d.DonorUser); using var admin=Client(host,d.Admin);
        Assert.Contains("Assign courier",await admin.GetStringAsync("/Courier/AssignCourier"));
        var codes=await donor.GetStringAsync("/Courier/Codes");
        var issued=await donor.PostAsync("/Courier/IssueCode",new FormUrlEncodedContent(new Dictionary<string,string>{
            ["claimId"]=d.Claim.Id.ToString(),["handoverType"]="Pickup",["__RequestVerificationToken"]=AntiForgery(codes)}));
        Assert.Equal(System.Net.HttpStatusCode.OK,issued.StatusCode);
        Assert.True(issued.Headers.CacheControl!.NoStore);
        var codeHtml=await issued.Content.ReadAsStringAsync();
        var raw=System.Text.RegularExpressions.Regex.Match(codeHtml,">([A-F0-9]{64})</pre>").Groups[1].Value;
        Assert.Equal(64,raw.Length);
        var page=await courier.GetStringAsync("/Courier/VerifyHandover?claimId="+d.Claim.Id);
        Assert.Contains("name=\"claimId\"",page); Assert.Contains("name=\"handoverType\"",page); Assert.Contains("name=\"handoverToken\"",page);
        var fields=new Dictionary<string,string>{["claimId"]=d.Claim.Id.ToString(),["handoverType"]="Pickup",["handoverToken"]=raw};
        var missingToken=await courier.PostAsync("/Courier/VerifyHandover",new FormUrlEncodedContent(fields));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest,missingToken.StatusCode);
        fields["__RequestVerificationToken"]=AntiForgery(page);
        var posted=await courier.PostAsync("/Courier/VerifyHandover",new FormUrlEncodedContent(fields));
        Assert.Equal(System.Net.HttpStatusCode.Redirect,posted.StatusCode);
        await db.Entry(d.Claim).ReloadAsync(); Assert.Equal(ClaimStatus.InTransit,d.Claim.Status);
    }

    [Theory]
    [InlineData("Donor")]
    [InlineData("Beneficiary")]
    public async Task Registration_assigns_only_selected_role_and_login_obeys_organization_status(string role)
    {
        using var host = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.UseEnvironment("Development").UseSetting("ConnectionStrings:DefaultConnection",fixture.ConnectionString));
        await using (var scope=host.Services.CreateAsyncScope())
        {
            var roles=scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>>();
            if (!await roles.RoleExistsAsync(role)) Assert.True((await roles.CreateAsync(new(role))).Succeeded);
        }
        using var client=host.CreateClient(new(){AllowAutoRedirect=false,BaseAddress=new Uri("https://localhost")});
        string email=Guid.NewGuid()+"@registration.local";
        // Test-only password is never used for a real account.
        const string password="Test-Only!48269";
        var form=await client.GetStringAsync("/Auth/Register");
        var response=await client.PostAsync("/Auth/Register",new FormUrlEncodedContent(new Dictionary<string,string>{
            ["orgName"]="Registration test",["licenseNumber"]=Guid.NewGuid().ToString(),["orgType"]=role,["email"]=email,["password"]=password,
            ["__RequestVerificationToken"]=AntiForgery(form)}));
        Assert.Equal(System.Net.HttpStatusCode.Redirect,response.StatusCode);
        await using var db=fixture.CreateContext();
        var user=await db.Users.Include(x=>x.Organization).SingleAsync(x=>x.Email==email);
        Assert.Equal(OrganizationStatus.Pending,user.Organization!.Status);
        var assigned=await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where ur.UserId==user.Id select r.Name).ToListAsync();
        Assert.Equal(new[]{role},assigned);
        async Task<System.Net.HttpStatusCode> Login()
        {
            var html=await client.GetStringAsync("/Auth/Login");
            return (await client.PostAsync("/Auth/Login",new FormUrlEncodedContent(new Dictionary<string,string>{
                ["email"]=email,["password"]=password,["__RequestVerificationToken"]=AntiForgery(html)}))).StatusCode;
        }
        Assert.Equal(System.Net.HttpStatusCode.OK,await Login());
        user.Organization.Status=OrganizationStatus.Active; await db.SaveChangesAsync();
        Assert.Equal(System.Net.HttpStatusCode.Redirect,await Login());
        user.Organization.Status=OrganizationStatus.Rejected; await db.SaveChangesAsync();
        Assert.Equal(System.Net.HttpStatusCode.OK,await Login());
        user.Organization.Status=OrganizationStatus.Suspended; await db.SaveChangesAsync();
        Assert.Equal(role=="Beneficiary"?System.Net.HttpStatusCode.Redirect:System.Net.HttpStatusCode.OK,await Login());
    }
    [Fact]
    public async Task Concurrent_organization_decisions_persist_one_audit()
    {
        await using var setup=fixture.CreateContext(); var d=await Seed(setup); d.Donor.Status=OrganizationStatus.Pending; await setup.SaveChangesAsync();
        await using var a=fixture.CreateContext(); await using var b=fixture.CreateContext(); var gate=new Gate();
        OrganizationApprovalService Make(ApplicationDbContext db) => new(d.Admin,new Repository<Organization>(db),new AuditService(db,d.Admin,TimeProvider.System),new GatedSave(db,gate));
        var results=await Task.WhenAll(Make(a).DecideAsync(d.Donor.Id,true,default),Make(b).DecideAsync(d.Donor.Id,false,default));
        Assert.Single(results,x=>x==null);
        await using var check=fixture.CreateContext();
        Assert.Equal(1,await check.AuditLogs.CountAsync(x=>x.EntityId==d.Donor.Id));
    }
    [Fact]
    public async Task Concurrent_publish_returns_one_controlled_conflict()
    {
        await using var setup=fixture.CreateContext(); var d=await Seed(setup); d.Claim.FoodDonation.Status=DonationStatus.Draft; await setup.SaveChangesAsync();
        await using var a=fixture.CreateContext(); await using var b=fixture.CreateContext(); var gate=new Gate();
        DonationService Make(ApplicationDbContext db) => new(d.DonorUser,new FoodDonationRepository(db,TimeProvider.System),new FoodCategoryRepository(db),new Repository<Organization>(db),new GatedSave(db,gate),new AuditService(db,d.DonorUser,TimeProvider.System),TimeProvider.System);
        var results=await Task.WhenAll(Make(a).PublishAsync(d.Claim.FoodDonationId),Make(b).PublishAsync(d.Claim.FoodDonationId));
        Assert.Single(results,x=>x.Succeeded);
        await using var check=fixture.CreateContext();
        Assert.Equal(1,await check.AuditLogs.CountAsync(x=>x.EntityId==d.Claim.FoodDonationId && x.Action=="DonationPublished"));
    }
}
