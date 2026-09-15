using System.Security.Claims;
using FoodLoop.Application.Claims;
using FoodLoop.Application.Exceptions;
using FoodLoop.Application.Identity;
using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Domain.Enums;
using FoodLoop.Web.Controllers;
using FoodLoop.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Foundation.Tests;
// Controller tests share the SQL fixture and seeding helpers of ClaimServiceTests: the controller runs against the real ClaimService.
public sealed partial class ClaimServiceTests
{
    private async Task<(IActionResult Result, ITempDataDictionary TempData)> InvokeAsync(
        ClaimsPrincipal principal, Func<ClaimsController, Task<IActionResult>> action, IUnitOfWork? unitOfWork = null)
    {
        await using var db = fixture.CreateContext();
        var httpContext = new DefaultHttpContext { User = principal };
        using var controller = new ClaimsController(Service(db, principal, unitOfWork))
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new NullTempDataProvider())
        };
        return (await action(controller), controller.TempData);
    }
    private Task<(IActionResult Result, ITempDataDictionary TempData)> PostCreateAsync(ClaimsPrincipal principal, Guid donationId, IUnitOfWork? unitOfWork = null)
        => InvokeAsync(principal, c => c.Create(donationId, CancellationToken.None), unitOfWork);
    private async Task<IActionResult> GetMineAsync(ClaimsPrincipal principal, int page = 1, int pageSize = 20)
        => (await InvokeAsync(principal, c => c.Mine(page, pageSize, CancellationToken.None))).Result;
    private static void AssertRedirectedToMine((IActionResult Result, ITempDataDictionary TempData) response, string key, string message)
    {
        var redirect = Assert.IsType<RedirectToActionResult>(response.Result);
        Assert.Equal(nameof(ClaimsController.Mine), redirect.ActionName); Assert.Null(redirect.ControllerName);
        Assert.Equal(message, response.TempData[key]);
    }
    private static MyClaimsViewModel AssertMineView(IActionResult result)
        => Assert.IsType<MyClaimsViewModel>(Assert.IsType<ViewResult>(result).Model);

    // ---- Controller: Create claim
    [Fact]
    public async Task Controller_create_success_redirects_to_mine_and_books_for_current_organization()
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync(); var donationId = await SeedDonationAsync();
        var response = await PostCreateAsync(Principal(userId), donationId);
        AssertRedirectedToMine(response, "Success", "Donation claimed.");
        await AssertPersistedAsync(donationId, DonationStatus.Claimed, claims: 1, activeClaims: 1, claimCreatedAudits: 1);
        await using var db = fixture.CreateContext();
        Assert.Equal(organizationId, (await db.DonationClaims.AsNoTracking().SingleAsync(x => x.FoodDonationId == donationId)).BeneficiaryOrganizationId);
    }
    [Fact]
    public void Controller_actions_accept_no_organization_ownership_or_status_input()
    {
        static string[] Inputs(string action) => [.. typeof(ClaimsController).GetMethod(action)!.GetParameters()
            .Where(x => x.ParameterType != typeof(CancellationToken)).Select(x => $"{x.ParameterType.Name} {x.Name}")];
        Assert.Equal(["Guid donationId"], Inputs(nameof(ClaimsController.Create)));
        Assert.Equal(["Int32 page", "Int32 pageSize"], Inputs(nameof(ClaimsController.Mine)));
    }
    [Fact]
    public async Task Controller_create_challenges_unauthenticated_user()
    {
        var donationId = await SeedDonationAsync();
        Assert.IsType<ChallengeResult>((await PostCreateAsync(new ClaimsPrincipal(new ClaimsIdentity()), donationId)).Result);
        await AssertRejectedWithoutWritesAsync(donationId);
    }
    [Theory]
    [InlineData(AppRoles.Donor)]
    [InlineData(AppRoles.Courier)]
    [InlineData(AppRoles.Admin)]
    public async Task Controller_create_forbids_wrong_role(string role)
    {
        var (userId, _) = await SeedBeneficiaryAsync(); var donationId = await SeedDonationAsync();
        Assert.IsType<ForbidResult>((await PostCreateAsync(Principal(userId, role), donationId)).Result);
        await AssertRejectedWithoutWritesAsync(donationId);
    }
    [Theory]
    [InlineData(OrganizationStatus.Pending)]
    [InlineData(OrganizationStatus.Rejected)]
    [InlineData(OrganizationStatus.Suspended)]
    public async Task Controller_create_forbids_inactive_beneficiary(OrganizationStatus status)
    {
        var (userId, _) = await SeedBeneficiaryAsync(status); var donationId = await SeedDonationAsync();
        Assert.IsType<ForbidResult>((await PostCreateAsync(Principal(userId), donationId)).Result);
        await AssertRejectedWithoutWritesAsync(donationId);
    }
    [Fact]
    public async Task Controller_create_reports_inactive_donor()
    {
        var (userId, _) = await SeedBeneficiaryAsync(); var donationId = await SeedDonationAsync(donorStatus: OrganizationStatus.Suspended);
        AssertRedirectedToMine(await PostCreateAsync(Principal(userId), donationId), "Error", "This donation cannot be claimed right now.");
        await AssertRejectedWithoutWritesAsync(donationId);
    }
    [Fact]
    public async Task Controller_create_returns_not_found_for_missing_donation()
    {
        var (userId, _) = await SeedBeneficiaryAsync();
        Assert.IsType<NotFoundResult>((await PostCreateAsync(Principal(userId), Guid.NewGuid())).Result);
    }
    [Fact]
    public async Task Controller_create_reports_unavailable_donation()
    {
        var (userId, _) = await SeedBeneficiaryAsync(); var donationId = await SeedDonationAsync(DonationStatus.Claimed);
        AssertRedirectedToMine(await PostCreateAsync(Principal(userId), donationId), "Error", "This donation is no longer available.");
        await AssertRejectedWithoutWritesAsync(donationId, DonationStatus.Claimed);
    }
    [Fact]
    public async Task Controller_create_reports_expired_donation()
    {
        var (userId, _) = await SeedBeneficiaryAsync(); var donationId = await SeedDonationAsync(expiresAtUtc: Now.AddMinutes(-1));
        AssertRedirectedToMine(await PostCreateAsync(Principal(userId), donationId), "Error", "This donation has expired.");
        await AssertRejectedWithoutWritesAsync(donationId);
    }
    [Fact]
    public async Task Controller_create_reports_conflict_for_existing_active_claim()
    {
        var (userId, _) = await SeedBeneficiaryAsync(); var donationId = await SeedDonationAsync(existingClaims: ClaimStatus.Booked);
        AssertRedirectedToMine(await PostCreateAsync(Principal(userId), donationId), "Error", "This donation was just claimed by someone else.");
        await AssertPersistedAsync(donationId, DonationStatus.Available, claims: 1, activeClaims: 1, claimCreatedAudits: 0);
    }
    [Fact]
    public async Task Controller_create_persistence_conflict_shows_controlled_message_without_database_details()
    {
        var (userId, _) = await SeedBeneficiaryAsync(); var donationId = await SeedDonationAsync();
        var response = await PostCreateAsync(Principal(userId), donationId, new ConflictingUnitOfWork());
        AssertRedirectedToMine(response, "Error", "This donation was just claimed by someone else.");
        var shown = string.Join(" ", response.TempData.Values);
        foreach (var secret in new[] { "UX_Claim", "Sql", "constraint", "Exception", "RowVersion" }) Assert.DoesNotContain(secret, shown, StringComparison.OrdinalIgnoreCase);
        await AssertRejectedWithoutWritesAsync(donationId);
    }

    // ---- Controller: My Claims
    [Theory]
    [InlineData(OrganizationStatus.Active)]
    [InlineData(OrganizationStatus.Suspended)]
    public async Task Controller_mine_shows_only_current_organizations_claims(OrganizationStatus status)
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync(status); var (_, otherOrg) = await SeedBeneficiaryAsync();
        var claimId = await SeedClaimAsync(organizationId!.Value, Now); await SeedClaimAsync(otherOrg!.Value, Now);
        var model = AssertMineView(await GetMineAsync(Principal(userId)));
        Assert.Equal(claimId, Assert.Single(model.Claims).ClaimId);
        Assert.Equal((1, 20), (model.Page, model.PageSize));
    }
    [Fact]
    public async Task Controller_mine_returns_valid_empty_model_when_organization_has_no_claims()
    {
        var (userId, _) = await SeedBeneficiaryAsync(); var (_, otherOrg) = await SeedBeneficiaryAsync();
        await SeedClaimAsync(otherOrg!.Value, Now);
        var view = Assert.IsType<ViewResult>(await GetMineAsync(Principal(userId)));
        Assert.Null(view.ViewName); // Default convention resolves Views/Claims/Mine.cshtml.
        var model = Assert.IsType<MyClaimsViewModel>(view.Model);
        Assert.Empty(model.Claims);
        Assert.Equal((1, 20), (model.Page, model.PageSize));
    }
    [Fact]
    public void My_claims_view_model_exposes_no_organization_data()
    {
        var properties = typeof(MyClaimsViewModel).GetProperties().Concat(typeof(ClaimSummary).GetProperties()).Select(x => x.Name);
        Assert.DoesNotContain(properties, x => x.Contains("Organization", StringComparison.OrdinalIgnoreCase));
    }
    [Theory]
    [InlineData(OrganizationStatus.Pending)]
    [InlineData(OrganizationStatus.Rejected)]
    public async Task Controller_mine_forbids_pending_or_rejected_beneficiary(OrganizationStatus status)
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync(status); await SeedClaimAsync(organizationId!.Value, Now);
        Assert.IsType<ForbidResult>(await GetMineAsync(Principal(userId)));
    }
    [Theory]
    [InlineData(AppRoles.Donor)]
    [InlineData(AppRoles.Courier)]
    [InlineData(AppRoles.Admin)]
    public async Task Controller_mine_forbids_wrong_role(string role)
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync(); await SeedClaimAsync(organizationId!.Value, Now);
        Assert.IsType<ForbidResult>(await GetMineAsync(Principal(userId, role)));
    }
    [Fact]
    public async Task Controller_mine_challenges_unauthenticated_and_forbids_user_without_beneficiary_organization()
    {
        Assert.IsType<ChallengeResult>(await GetMineAsync(new ClaimsPrincipal(new ClaimsIdentity())));
        var (userId, _) = await SeedUserAsync(null);
        Assert.IsType<ForbidResult>(await GetMineAsync(Principal(userId)));
    }
    [Fact]
    public async Task Controller_mine_passes_valid_paging_to_the_service()
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync();
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++) ids.Add(await SeedClaimAsync(organizationId!.Value, Now.AddMinutes(-i)));
        var model = AssertMineView(await GetMineAsync(Principal(userId), page: 2, pageSize: 2));
        Assert.Equal((2, 2), (model.Page, model.PageSize));
        Assert.Equal(ids[2..4], [.. model.Claims.Select(x => x.ClaimId)]);
    }
    [Theory]
    [InlineData(0, 20, 1, 20)]
    [InlineData(-5, 20, 1, 20)]
    [InlineData(1, 0, 1, 1)]
    [InlineData(1, -1, 1, 1)]
    [InlineData(1, 101, 1, 100)]
    [InlineData(1, int.MaxValue, 1, 100)]
    public async Task Controller_mine_normalizes_invalid_paging_instead_of_failing(int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync(); await SeedClaimAsync(organizationId!.Value, Now);
        var model = AssertMineView(await GetMineAsync(Principal(userId), page, pageSize));
        Assert.Equal((expectedPage, expectedPageSize), (model.Page, model.PageSize));
        Assert.Single(model.Claims);
    }

    [Fact]
    public async Task Controller_mine_extreme_page_returns_valid_empty_model_without_changes()
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync(); var (_, otherOrg) = await SeedBeneficiaryAsync();
        var ownClaim = await SeedClaimAsync(organizationId!.Value, Now); var otherClaim = await SeedClaimAsync(otherOrg!.Value, Now);

        var model = AssertMineView(await GetMineAsync(Principal(userId), page: int.MaxValue, pageSize: 20));
        Assert.Empty(model.Claims);
        Assert.Equal((int.MaxValue, 20), (model.Page, model.PageSize));

        await using var db = fixture.CreateContext();
        foreach (var claimId in new[] { ownClaim, otherClaim })
        {
            var claim = await db.DonationClaims.AsNoTracking().Include(x => x.FoodDonation).SingleAsync(x => x.Id == claimId);
            Assert.Equal(ClaimStatus.Booked, claim.Status); Assert.Equal(DonationStatus.Claimed, claim.FoodDonation.Status);
        }
        Assert.False(await db.AuditLogs.AnyAsync(x => x.ActorUserId == userId));
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
    private sealed class ConflictingUnitOfWork : IUnitOfWork
    {
        // Simulates the lost race: the message carries database internals that must never reach the user.
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new PersistenceConflictException(
            "Cannot insert duplicate key row in object 'dbo.DonationClaims' with unique index 'UX_Claim_ActiveDonation'.",
            new InvalidOperationException("SqlException 2601: unique constraint violation; RowVersion mismatch"));
        public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
