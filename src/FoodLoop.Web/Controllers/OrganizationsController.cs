using FoodLoop.Application.Organizations;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace FoodLoop.Web.Controllers;
[Authorize(Roles = "Admin")]
public sealed class OrganizationsController(ApplicationDbContext db, OrganizationApprovalService approvals) : Controller
{
    [HttpGet]
    public async Task<IActionResult> PendingRequests(CancellationToken ct) => View(await db.Organizations.AsNoTracking().Where(x => x.Status == OrganizationStatus.Pending).OrderBy(x => x.CreatedAtUtc).ToListAsync(ct));
    [HttpPost]
    public Task<IActionResult> ApproveOrganization(Guid id, CancellationToken ct) => Decide(id, true, ct);
    [HttpPost]
    public Task<IActionResult> RejectOrganization(Guid id, CancellationToken ct) => Decide(id, false, ct);
    private async Task<IActionResult> Decide(Guid id, bool approve, CancellationToken ct)
    {
        var error = await approvals.DecideAsync(id, approve, ct);
        TempData[error == null ? "SuccessMessage" : "ErrorMessage"] = error ?? "Organization request updated.";
        return RedirectToAction(nameof(PendingRequests));
    }
}
