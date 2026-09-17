using FoodLoop.Application.Organizations;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace FoodLoop.Web.Controllers;
[Authorize(Roles = "Admin")]
public sealed class OrganizationsController(
    ApplicationDbContext db,
    OrganizationApprovalService approvals,
    OrganizationManagementService management) : Controller
{
    [HttpGet]
    public async Task<IActionResult> PendingRequests(CancellationToken ct) =>
        View(await db.Organizations.AsNoTracking()
            .Where(x => x.Status == OrganizationStatus.Pending)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ApproveOrganization(Guid id, CancellationToken ct) => Decide(id, true, ct);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RejectOrganization(Guid id, CancellationToken ct) => Decide(id, false, ct);

    [HttpGet]
    public async Task<IActionResult> Manage(OrganizationStatus? status = null, int page = 1, CancellationToken ct = default) =>
        View(await management.GetPageAsync(status, page, ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Suspend(Guid id, OrganizationStatus? status = null, int page = 1, CancellationToken ct = default) =>
        ChangeStatus(id, true, status, page, ct);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Reactivate(Guid id, OrganizationStatus? status = null, int page = 1, CancellationToken ct = default) =>
        ChangeStatus(id, false, status, page, ct);

    private async Task<IActionResult> Decide(Guid id, bool approve, CancellationToken ct)
    {
        var error = await approvals.DecideAsync(id, approve, ct);
        TempData[error == null ? "SuccessMessage" : "ErrorMessage"] = error ?? "Organization request updated.";
        return RedirectToAction(nameof(PendingRequests));
    }

    private async Task<IActionResult> ChangeStatus(
        Guid id,
        bool suspend,
        OrganizationStatus? status,
        int page,
        CancellationToken ct)
    {
        var result = suspend
            ? await management.SuspendAsync(id, ct)
            : await management.ReactivateAsync(id, ct);
        if (result.Outcome == OrganizationStatusChangeOutcome.NotFound) return NotFound();

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Succeeded
            ? suspend ? "Organization suspended." : "Organization reactivated."
            : result.Error ?? "Organization status could not be changed.";
        return RedirectToAction(nameof(Manage), new { status, page });
    }
}
