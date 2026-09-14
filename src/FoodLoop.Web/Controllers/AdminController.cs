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
    public async Task<IActionResult> Audit(int page = 1, CancellationToken cancellationToken = default)
        => View(await service.GetAuditPageAsync(page, cancellationToken));
    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }
}
