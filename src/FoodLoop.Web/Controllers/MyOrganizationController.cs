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
    public async Task<IActionResult> Index(Guid id, CancellationToken ct)
    {
        var org = await profileService.GetMyOrganizationAsync(id, ct);

        if (org == null)
        {
            TempData["ErrorMessage"] = "Your account is not associated with an Organization or access was denied.";
            return RedirectToAction("Index", "Home");
        }

        var vm = new OrganizationProfileViewModel
        {
            Id = org.Id,
            Name = org.Name,
            LicenseNumber = org.LicenseNumber,
            Type = org.Type,
            Status = org.Status,
            Address = org.Address,
            RowVersion = org.RowVersion
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(OrganizationProfileViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Index", model);

        var error = await profileService.UpdateProfileAsync(
            model.Id,
            model.Name,
            model.Address,
            model.RowVersion,
            ct);

        if (error != null)
        {
            TempData["ErrorMessage"] = error;
            return RedirectToAction(nameof(Index), new { id = model.Id });
        }

        TempData["SuccessMessage"] = "Organization profile updated successfully.";
        return RedirectToAction(nameof(Index), new { id = model.Id });
    }
}