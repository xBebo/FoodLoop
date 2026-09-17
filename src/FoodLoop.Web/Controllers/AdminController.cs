using FoodLoop.Application.Admin;
using FoodLoop.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace FoodLoop.Web.Controllers;
[Authorize(Roles = AppRoles.Admin)]
public sealed class AdminController(IAdminService service) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => View(await service.GetDashboardAsync(cancellationToken));
    [HttpGet]
    public async Task<IActionResult> Audit(int page = 1, string? actionName = null, string? actor = null,
        DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var filter = new AuditFilter(
            string.IsNullOrWhiteSpace(actionName) ? null : actionName.Trim(),
            string.IsNullOrWhiteSpace(actor) ? null : actor.Trim(),
            from.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(from.Value, DateTimeKind.Utc)) : null,
            to.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(to.Value, DateTimeKind.Utc)) : null);
        try
        {
            return View(await service.GetAuditPageAsync(page, filter, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(new AuditPage(Array.Empty<AuditEntry>(), 1, 20, 0) { Filter = filter });
        }
    }
    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }
}
