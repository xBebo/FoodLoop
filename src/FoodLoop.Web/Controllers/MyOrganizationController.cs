using System.Threading;
using System.Threading.Tasks;
using FoodLoop.Application.Organizations;
using FoodLoop.Domain.Enums;
using FoodLoop.Web.Models.Organizations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodLoop.Web.Controllers;

[Authorize]
public sealed class MyOrganizationController(OrganizationProfileService profileService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? returnUrl = null, CancellationToken ct = default)
    {
        var org = await profileService.GetMyOrganizationAsync(ct);
        if (org == null)
        {
            return Forbid();
        }

        ViewData["ReturnUrl"] = returnUrl;

        var model = new OrganizationProfileViewModel
        {
            Id = org.Id,
            Name = org.Name,
            Address = org.Address,
            LicenseNumber = org.LicenseNumber,
            Type = org.Type,
            Status = org.Status,
            IsReadOnly = org.Status == OrganizationStatus.Suspended,
            RowVersion = org.RowVersion
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(OrganizationProfileViewModel model, string? returnUrl = null, CancellationToken ct = default)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        var error = await profileService.UpdateProfileAsync(model.Name, model.Address, model.RowVersion, ct);
        if (error != null)
        {
            TempData["ErrorMessage"] = error;
            return RedirectToAction(nameof(Index), new { returnUrl });
        }

        TempData["SuccessMessage"] = "Profile updated successfully.";

        // التحقق الأمني لمنع Open Redirect
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }
}
