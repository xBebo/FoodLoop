using System.Security.Claims;
using System.Text.RegularExpressions;
using FoodLoop.Application.Claims;
using FoodLoop.Application.Exceptions;
using FoodLoop.Application.Identity;
using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Domain.Entities;
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
        Assert.Equal(["Guid claimId"], Inputs(nameof(ClaimsController.Cancel)));
        Assert.Equal(["Int32 page", "Int32 pageSize"], Inputs(nameof(ClaimsController.Mine)));
        Assert.Equal(["Guid claimId"], Inputs(nameof(ClaimsController.Details)));
        Assert.Equal([typeof(Guid), typeof(CancellationToken)],
            typeof(ClaimsController).GetMethod(nameof(ClaimsController.Details))!.GetParameters().Select(x => x.ParameterType));
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

    // ---- Controller: Cancel claim
    private Task<(IActionResult Result, ITempDataDictionary TempData)> PostCancelAsync(ClaimsPrincipal principal, Guid claimId, IUnitOfWork? unitOfWork = null)
        => InvokeAsync(principal, c => c.Cancel(claimId, CancellationToken.None), unitOfWork);
    // Unchanged RowVersions and audit count prove the rejected POST wrote nothing.
    private async Task<(IActionResult Result, ITempDataDictionary TempData)> PostCancelRejectedAsync(ClaimsPrincipal principal, Guid claimId, IUnitOfWork? unitOfWork = null)
    {
        var before = await ReadStateAsync(claimId);
        var response = await PostCancelAsync(principal, claimId, unitOfWork);
        Assert.Equal(before, await ReadStateAsync(claimId));
        return response;
    }

    [Fact]
    public async Task Controller_cancel_success_redirects_to_mine_and_returns_donation_to_marketplace()
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync(); var (claimId, donationId) = await SeedClaimForCancelAsync(organizationId!.Value);
        AssertRedirectedToMine(await PostCancelAsync(Principal(userId), claimId), "Success", "Claim cancelled. The donation is available again.");
        await AssertCancelledAsync(claimId, donationId, userId, DonationStatus.Available);
    }
    [Fact]
    public async Task Controller_cancel_success_is_honest_when_donation_returns_to_draft()
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync();
        var (claimId, donationId) = await SeedClaimForCancelAsync(organizationId!.Value, expiresAtUtc: Now.AddMinutes(-1));
        AssertRedirectedToMine(await PostCancelAsync(Principal(userId), claimId), "Success", "Claim cancelled. The donation was returned to the donor as a draft.");
        await AssertCancelledAsync(claimId, donationId, userId, DonationStatus.Draft);
    }
    [Fact]
    public async Task Controller_cancel_reports_not_cancellable_claim()
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync(); var (courierId, _) = await SeedUserAsync(null);
        var (claimId, _) = await SeedClaimForCancelAsync(organizationId!.Value, courierUserId: courierId);
        AssertRedirectedToMine(await PostCancelRejectedAsync(Principal(userId), claimId), "Error", "This claim can no longer be cancelled.");
    }
    [Fact]
    public async Task Controller_cancel_persistence_conflict_shows_controlled_message_without_database_details()
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync(); var (claimId, _) = await SeedClaimForCancelAsync(organizationId!.Value);
        var response = await PostCancelRejectedAsync(Principal(userId), claimId, new ConflictingUnitOfWork());
        AssertRedirectedToMine(response, "Error", "This claim just changed. Refresh and try again.");
        var shown = string.Join(" ", response.TempData.Values);
        foreach (var secret in new[] { "UX_Claim", "Sql", "constraint", "Exception", "RowVersion", "dbo." }) Assert.DoesNotContain(secret, shown, StringComparison.OrdinalIgnoreCase);
    }
    [Theory]
    [InlineData(OrganizationStatus.Active)]
    [InlineData(OrganizationStatus.Suspended)]
    public async Task Controller_cancel_of_foreign_or_missing_claim_is_the_same_not_found(OrganizationStatus callerStatus)
    {
        var (userId, _) = await SeedBeneficiaryAsync(callerStatus); var (_, otherOrganizationId) = await SeedBeneficiaryAsync();
        var (foreignClaimId, _) = await SeedClaimForCancelAsync(otherOrganizationId!.Value);
        var foreign = await PostCancelRejectedAsync(Principal(userId), foreignClaimId);
        var missing = await PostCancelAsync(Principal(userId), Guid.NewGuid());
        Assert.IsType<NotFoundResult>(foreign.Result); Assert.IsType<NotFoundResult>(missing.Result);
        Assert.Empty(foreign.TempData); Assert.Empty(missing.TempData); // no message that could hint the id exists
        await using var db = fixture.CreateContext();
        Assert.False(await db.AuditLogs.AnyAsync(x => x.ActorUserId == userId));
    }
    [Fact]
    public async Task Controller_cancel_challenges_unauthenticated_user()
    {
        var (_, organizationId) = await SeedBeneficiaryAsync(); var (claimId, _) = await SeedClaimForCancelAsync(organizationId!.Value);
        Assert.IsType<ChallengeResult>((await PostCancelRejectedAsync(new ClaimsPrincipal(new ClaimsIdentity()), claimId)).Result);
    }
    [Theory]
    [InlineData(AppRoles.Donor)]
    [InlineData(AppRoles.Courier)]
    [InlineData(AppRoles.Admin)]
    public async Task Controller_cancel_forbids_wrong_role(string role)
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync(); var (claimId, _) = await SeedClaimForCancelAsync(organizationId!.Value);
        Assert.IsType<ForbidResult>((await PostCancelRejectedAsync(Principal(userId, role), claimId)).Result);
    }
    [Theory]
    [InlineData(OrganizationStatus.Pending)]
    [InlineData(OrganizationStatus.Rejected)]
    [InlineData(OrganizationStatus.Suspended)]
    public async Task Controller_cancel_forbids_inactive_beneficiary(OrganizationStatus status)
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync(status); var (claimId, _) = await SeedClaimForCancelAsync(organizationId!.Value);
        Assert.IsType<ForbidResult>((await PostCancelRejectedAsync(Principal(userId), claimId)).Result);
    }
    [Fact]
    public async Task Suspended_beneficiary_my_claims_is_read_only_and_direct_cancel_post_is_forbidden()
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync(OrganizationStatus.Suspended);
        var (claimId, _) = await SeedClaimForCancelAsync(organizationId!.Value); // would be cancellable for an Active organization

        var model = AssertMineView(await GetMineAsync(Principal(userId)));
        var summary = Assert.Single(model.Claims);
        Assert.Equal((claimId, ClaimStatus.Booked, false), (summary.ClaimId, summary.ClaimStatus, summary.CanCancel));
        var html = await WebAppTests.RenderMineAsync(web, model);
        Assert.Contains("Cancellable food", html);
        Assert.DoesNotContain("/Claims/Cancel", html);
        Assert.DoesNotContain("Cancel claim", html);
        // The claim id may appear only in its read-only Details link, never in a Cancel form input.
        Assert.DoesNotContain("<form", html);
        Assert.DoesNotContain($"name=\"claimId\" value=\"{claimId}\"", html);
        Assert.Contains($"href=\"/Claims/Details?claimId={claimId}\"", html);

        Assert.IsType<ForbidResult>((await PostCancelRejectedAsync(Principal(userId), claimId)).Result);
        await using var db = fixture.CreateContext();
        Assert.False(await db.AuditLogs.AnyAsync(x => x.Action == "ClaimCancelled" && x.EntityId == claimId));
    }
    [Fact]
    public async Task Active_beneficiary_my_claims_renders_cancel_only_for_the_eligible_claim()
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync(); var (courierId, _) = await SeedUserAsync(null);
        var (eligible, _) = await SeedClaimForCancelAsync(organizationId!.Value);
        var (assigned, _) = await SeedClaimForCancelAsync(organizationId.Value, courierUserId: courierId);
        var (delivered, _) = await SeedClaimForCancelAsync(organizationId.Value, ClaimStatus.Delivered, DonationStatus.Delivered);

        var html = await WebAppTests.RenderMineAsync(web, AssertMineView(await GetMineAsync(Principal(userId))));
        Assert.Single(Regex.Matches(html, "action=\"/Claims/Cancel\""));
        Assert.Contains($"name=\"claimId\" value=\"{eligible}\"", html);
        // Non-cancellable claims keep their Details link, but their id never reaches a Cancel form input.
        foreach (var claimId in new[] { assigned, delivered })
        {
            Assert.DoesNotContain($"name=\"claimId\" value=\"{claimId}\"", html);
            Assert.Single(Regex.Matches(html, claimId.ToString())); // the Details href only
        }
        foreach (var claimId in new[] { eligible, assigned, delivered }) Assert.Contains($"href=\"/Claims/Details?claimId={claimId}\"", html);
    }

    // ---- Controller: Claim Details
    private async Task<IActionResult> GetDetailsAsync(ClaimsPrincipal principal, Guid claimId)
        => (await InvokeAsync(principal, c => c.Details(claimId, CancellationToken.None))).Result;

    [Fact]
    public void Controller_details_is_a_get_only_action()
    {
        var method = typeof(ClaimsController).GetMethod(nameof(ClaimsController.Details))!;
        Assert.Single(method.GetCustomAttributes(typeof(HttpGetAttribute), false));
        Assert.Empty(method.GetCustomAttributes(typeof(HttpPostAttribute), false));
    }
    [Theory]
    [InlineData(OrganizationStatus.Active)]
    [InlineData(OrganizationStatus.Suspended)]
    public async Task Controller_details_renders_default_view_for_own_claim_and_is_read_only(OrganizationStatus status)
    {
        var (userId, claimId, _) = await SeedOwnClaimAsync(status);
        var view = Assert.IsType<ViewResult>(await GetDetailsAsync(Principal(userId), claimId));
        Assert.Null(view.ViewName); // Default convention resolves Views/Claims/Details.cshtml.
        var model = Assert.IsType<ClaimDetails>(view.Model);
        Assert.Equal((claimId, ClaimStatus.Booked, ClaimedAt, status == OrganizationStatus.Active), (model.ClaimId, model.Status, model.ClaimedAtUtc, model.CanCancel));
        Assert.Equal([Claimed(ClaimedAt)], model.Timeline);

        var html = await WebAppTests.RenderDetailsAsync(web, model);
        Assert.NotEmpty(WebAppTests.PageBody(html));
        Assert.DoesNotContain("<form", WebAppTests.PageBody(html));
        Assert.DoesNotContain("<button", WebAppTests.PageBody(html));
        Assert.DoesNotContain("/Claims/Cancel", html);
        Assert.DoesNotContain("Cancel claim", html);
        Assert.Equal(status == OrganizationStatus.Active, html.Contains("can still be cancelled from"));
        await using var db = fixture.CreateContext();
        Assert.False(await db.AuditLogs.AnyAsync(x => x.ActorUserId == userId)); // reading writes nothing
    }
    [Fact]
    public async Task Controller_details_challenges_unauthenticated_user()
    {
        var (_, claimId, _) = await SeedOwnClaimAsync();
        Assert.IsType<ChallengeResult>(await GetDetailsAsync(new ClaimsPrincipal(new ClaimsIdentity()), claimId));
    }
    [Theory]
    [InlineData(AppRoles.Donor)]
    [InlineData(AppRoles.Courier)]
    [InlineData(AppRoles.Admin)]
    public async Task Controller_details_forbids_wrong_role(string role)
    {
        var (userId, claimId, _) = await SeedOwnClaimAsync();
        Assert.IsType<ForbidResult>(await GetDetailsAsync(Principal(userId, role), claimId));
    }
    [Fact]
    public async Task Controller_details_forbids_user_without_a_beneficiary_organization()
    {
        var (noOrganizationUser, _) = await SeedUserAsync(null); var (_, claimId, _) = await SeedOwnClaimAsync();
        Assert.IsType<ForbidResult>(await GetDetailsAsync(Principal(noOrganizationUser), claimId));
        var (donorLinkedUser, donorOrganizationId) = await SeedBeneficiaryAsync(type: OrganizationType.Donor);
        var (donorClaimId, _) = await SeedClaimForCancelAsync(donorOrganizationId!.Value);
        Assert.IsType<ForbidResult>(await GetDetailsAsync(Principal(donorLinkedUser), donorClaimId));
    }
    [Theory]
    [InlineData(OrganizationStatus.Pending)]
    [InlineData(OrganizationStatus.Rejected)]
    public async Task Controller_details_forbids_pending_or_rejected_beneficiary(OrganizationStatus status)
    {
        var (userId, claimId, _) = await SeedOwnClaimAsync(status);
        Assert.IsType<ForbidResult>(await GetDetailsAsync(Principal(userId), claimId));
    }
    [Theory]
    [InlineData(OrganizationStatus.Active)]
    [InlineData(OrganizationStatus.Suspended)]
    public async Task Controller_details_of_foreign_or_missing_claim_is_the_same_not_found(OrganizationStatus callerStatus)
    {
        var (userId, _) = await SeedBeneficiaryAsync(callerStatus);
        var (_, foreignClaimId, _) = await SeedOwnClaimAsync();
        var foreign = Assert.IsType<NotFoundResult>(await GetDetailsAsync(Principal(userId), foreignClaimId));
        var missing = Assert.IsType<NotFoundResult>(await GetDetailsAsync(Principal(userId), Guid.NewGuid()));
        Assert.Equal(StatusCodes.Status404NotFound, foreign.StatusCode); Assert.Equal(missing.StatusCode, foreign.StatusCode);
    }
    [Fact]
    public async Task Controller_details_rendered_html_exposes_no_identity_audit_qr_or_donor_data()
    {
        var courier = await SeedCourierAsync("Sam Courier");
        var (userId, claimId, donationId) = await SeedOwnClaimAsync(status: ClaimStatus.Closed, donationStatus: DonationStatus.Closed, courierUserId: courier.UserId);
        var secret = "SECRET-" + Guid.NewGuid().ToString("N");
        var tokenHash = Convert.ToHexString(Guid.NewGuid().ToByteArray()) + Convert.ToHexString(Guid.NewGuid().ToByteArray());
        Organization donor;
        await using (var db = fixture.CreateContext())
        {
            donor = (await db.FoodDonations.Include(x => x.DonorOrganization).SingleAsync(x => x.Id == donationId)).DonorOrganization;
            donor.Name = "Donor-" + Guid.NewGuid().ToString("N"); donor.Address = "Donor-address-" + Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync();
        }
        var evidenceIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray(); // canaries: internal evidence row ids
        var assigned = ClaimAudit(claimId, "CourierAssigned", Now.AddMinutes(-8), details: $"CourierId={courier.UserId}; DonationId={donationId}; {secret}");
        var other = ClaimAudit(claimId, "SomethingElse", Now.AddMinutes(-7), details: secret);
        var pickup = Handover(claimId, courier.UserId, HandoverType.Pickup, Now.AddMinutes(-5));
        var delivery = Handover(claimId, courier.UserId, HandoverType.Delivery, Now.AddMinutes(-2));
        (assigned.Id, other.Id, pickup.Id, delivery.Id) = (evidenceIds[0], evidenceIds[1], evidenceIds[2], evidenceIds[3]);
        await SeedEvidenceAsync(assigned, other, pickup, delivery,
            new QrVerificationToken { Id = evidenceIds[4], DonationClaimId = claimId, CourierUserId = courier.UserId, Purpose = QrPurpose.Delivery, TokenHash = tokenHash,
                CreatedAtUtc = Now.AddMinutes(-3), ExpiresAtUtc = Now.AddMinutes(10), UsedAtUtc = Now.AddMinutes(-2) });

        var model = Assert.IsType<ClaimDetails>(Assert.IsType<ViewResult>(await GetDetailsAsync(Principal(userId), claimId)).Model);
        var html = await WebAppTests.RenderDetailsAsync(web, model);
        Assert.Contains("Sam Courier", html);
        foreach (var label in new[] { "Claimed", "Courier assigned", "Pickup verified", "Delivery verified", "Closed" }) Assert.Contains(label, html);
        foreach (var value in new[] { courier.UserId.ToString(), courier.UserName, courier.Email, secret, "CourierId=", "DonationId=", tokenHash,
                     donor.Id.ToString(), donor.Name, donor.Address, donor.LicenseNumber, donationId.ToString(), claimId.ToString() })
            Assert.DoesNotContain(value, html, StringComparison.OrdinalIgnoreCase);
        foreach (var evidenceId in evidenceIds) // audit, handover and QR row ids stay internal
        {
            Assert.DoesNotContain(evidenceId.ToString(), html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(evidenceId.ToString("N"), html, StringComparison.OrdinalIgnoreCase);
        }
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
