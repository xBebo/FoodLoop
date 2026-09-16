using FoodLoop.Application.Courier;
using FoodLoop.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace FoodLoop.Web.Controllers;
[Authorize]
public sealed class CourierController(CourierService service) : Controller
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var result = await next();
        if (result.Exception is UnauthorizedAccessException) { result.Result = Forbid(); result.ExceptionHandled = true; }
    }
    [Authorize(Roles = "Admin"), HttpGet]
    public async Task<IActionResult> AssignCourier(CancellationToken ct)
    {
        ViewBag.Couriers = await service.CouriersAsync();
        return View(await service.AssignableAsync(ct));
    }
    [Authorize(Roles = "Admin"), HttpPost]
    public async Task<IActionResult> AssignCourier(Guid claimId, Guid courierUserId, CancellationToken ct)
    {
        var result = await service.AssignAsync(claimId, courierUserId, ct);
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Error ?? "Courier assigned.";
        return RedirectToAction(nameof(AssignCourier));
    }
    [Authorize(Roles = "Courier"), HttpGet]
    public async Task<IActionResult> MyTasks(CancellationToken ct) => View(await service.MyTasksAsync(ct));
    [Authorize(Roles = "Courier"), HttpGet]
    public async Task<IActionResult> VerifyHandover(Guid claimId, CancellationToken ct)
    {
        var claim = await service.MyTaskAsync(claimId, ct);
        return claim == null ? NotFound() : View(claim);
    }
    [Authorize(Roles = "Courier"), HttpPost]
    public async Task<IActionResult> VerifyHandover(Guid claimId, string? handoverToken, HandoverType handoverType, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest("Invalid handover input.");
        var result = await service.VerifyAsync(claimId, handoverToken, handoverType, ct);
        if (result.Succeeded) { TempData["SuccessMessage"] = "Handover verified."; return RedirectToAction(nameof(MyTasks)); }
        ModelState.AddModelError("", result.Error!);
        var claim = await service.MyTaskAsync(claimId, ct);
        return claim == null ? NotFound() : View(claim);
    }
    [Authorize(Roles = "Donor,Beneficiary"), HttpGet]
    public async Task<IActionResult> Codes(CancellationToken ct) => View(await service.OrganizationTasksAsync(ct));
    [Authorize(Roles = "Donor,Beneficiary"), HttpPost]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> IssueCode(Guid claimId, HandoverType handoverType, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest("Invalid handover input.");
        var result = await service.IssueAsync(claimId, handoverType, ct);
        if (!result.Succeeded) { TempData["ErrorMessage"] = result.Error; return RedirectToAction(nameof(Codes)); }
        return View("Code", result.Token);
    }
}
