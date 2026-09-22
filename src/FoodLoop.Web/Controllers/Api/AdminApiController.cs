using System.ComponentModel.DataAnnotations;
using FoodLoop.Application.Admin;
using FoodLoop.Application.Courier;
using FoodLoop.Application.Identity;
using FoodLoop.Application.Organizations;
using FoodLoop.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodLoop.Web.Controllers.Api;

public sealed record AssignCourierRequest([Required] Guid? CourierUserId);

// Actor ids and audit Details are deliberately absent: the name is the only actor information shown.
public sealed record AuditEntryResponse(Guid Id, string Action, string ActorName, string EntityType, Guid EntityId, DateTimeOffset TimestampUtc);

// Admin workspace. Queries and rules live in AdminService, OrganizationManagementService, OrganizationApprovalService and
// CourierService; this only maps their results. Unsafe methods are covered by the global antiforgery filter.
[ApiController]
[Route("api/admin")]
[Authorize(Roles = AppRoles.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminApiController(
    IAdminService admin, OrganizationManagementService organizations, OrganizationApprovalService approvals, CourierService couriers)
    : ControllerBase
{
    [HttpGet("dashboard")]
    public Task<DashboardSummary> Dashboard(CancellationToken ct) => admin.GetDashboardAsync(ct);

    [HttpGet("organizations")]
    public async Task<IActionResult> Organizations(OrganizationStatus? status, int page = 1, CancellationToken ct = default)
    {
        var result = await organizations.GetPageAsync(status, page, ct);
        return Ok(new { items = result.Items, result.Page, result.HasPrevious, result.HasNext, result.TotalCount });
    }

    [HttpGet("organizations/pending")]
    public Task<IReadOnlyList<PendingOrganizationItem>> Pending(CancellationToken ct) => organizations.GetPendingAsync(ct);

    [HttpPost("organizations/{id:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken ct) => StatusChange(await organizations.SuspendAsync(id, ct));

    [HttpPost("organizations/{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct) => StatusChange(await organizations.ReactivateAsync(id, ct));

    [HttpPost("organizations/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct) => StatusChange(await approvals.DecideAsync(id, true, ct));

    [HttpPost("organizations/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken ct) => StatusChange(await approvals.DecideAsync(id, false, ct));

    [HttpGet("claims/assignable")]
    public Task<IReadOnlyList<AssignableClaimItem>> Assignable(CancellationToken ct) => couriers.AssignableItemsAsync(ct);

    [HttpGet("couriers")]
    public Task<IReadOnlyList<CourierOption>> Couriers() => couriers.CouriersAsync();

    [HttpPost("claims/{claimId:guid}/courier")]
    public async Task<IActionResult> Assign(Guid claimId, AssignCourierRequest request, CancellationToken ct)
    {
        var result = await couriers.AssignAsync(claimId, request.CourierUserId!.Value, ct);
        return result.Succeeded ? NoContent() : result.Failure switch
        {
            CourierFailureKind.Validation => this.Fail(StatusCodes.Status400BadRequest, "courier.invalid"),
            CourierFailureKind.InvalidState => this.Fail(StatusCodes.Status409Conflict, "claim.not_assignable"),
            CourierFailureKind.Conflict => this.Fail(StatusCodes.Status409Conflict, "claim.conflict"),
            _ => this.Fail(StatusCodes.Status404NotFound, "claim.not_found")
        };
    }

    [HttpGet("audit")]
    public async Task<IActionResult> Audit(int page = 1, string? action = null, string? actor = null,
        DateTimeOffset? fromUtc = null, DateTimeOffset? toUtc = null, CancellationToken ct = default)
    {
        if (fromUtc >= toUtc) return this.Fail(StatusCodes.Status400BadRequest, "audit.invalid_range");
        var result = await admin.GetAuditPageAsync(page, new AuditFilter(
            string.IsNullOrWhiteSpace(action) ? null : action.Trim(), string.IsNullOrWhiteSpace(actor) ? null : actor.Trim(),
            fromUtc?.ToUniversalTime(), toUtc?.ToUniversalTime()), ct);
        return Ok(new
        {
            items = result.Items.Select(x => new AuditEntryResponse(x.Id, x.Action, x.ActorName, x.EntityType, x.EntityId, x.TimestampUtc)),
            result.Page, result.HasPrevious, result.HasNext, result.TotalCount
        });
    }

    private IActionResult StatusChange(OrganizationStatusChangeResult result) => result.Outcome switch
    {
        OrganizationStatusChangeOutcome.Updated => NoContent(),
        OrganizationStatusChangeOutcome.NotFound => this.Fail(StatusCodes.Status404NotFound, "organization.not_found"),
        OrganizationStatusChangeOutcome.InvalidState => this.Fail(StatusCodes.Status409Conflict, "organization.invalid_state"),
        _ => this.Fail(StatusCodes.Status409Conflict, "organization.conflict")
    };
}
