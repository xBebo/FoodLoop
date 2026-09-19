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
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var org = await profileService.GetMyOrganizationAsync(ct);
        if (org == null)
        {
            return Forbid();
        }

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
    public async Task<IActionResult> Edit(OrganizationProfileViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        var error = await profileService.UpdateProfileAsync(model.Name, model.Address, model.RowVersion, ct);
        if (error != null)
        {
            TempData["ErrorMessage"] = error;
            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = "Profile updated successfully.";
        return RedirectToAction(nameof(Index));
    }
}