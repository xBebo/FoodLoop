using System.Threading;
using System.Threading.Tasks;
using FoodLoop.Application.Organizations;
using FoodLoop.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodLoop.Web.Controllers;

[Authorize]
public sealed class MyOrganizationController(OrganizationProfileService profileService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var org = await profileService.GetMyOrganizationAsync(ct);
        if (org == null)
        {
            return Forbid();
        }

        ViewBag.IsReadOnly = org.Status == OrganizationStatus.Suspended;
        return View(org);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(string name, string address, byte[] rowVersion, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(address))
        {
            TempData["ErrorMessage"] = "Name and Address are required.";
            return RedirectToAction(nameof(Index));
        }

        var error = await profileService.UpdateProfileAsync(name, address, rowVersion, ct);
        if (error != null)
        {
            TempData["ErrorMessage"] = error;
        }
        else
        {
            TempData["SuccessMessage"] = "Profile updated successfully.";
        }

        return RedirectToAction(nameof(Index));
    }
}