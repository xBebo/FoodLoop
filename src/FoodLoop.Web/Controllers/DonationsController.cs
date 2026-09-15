using FoodLoop.Application.Donations;
using FoodLoop.Application.Identity;
using FoodLoop.Web.Models.Donations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FoodLoop.Web.Controllers;

[Authorize]
public sealed class DonationsController(DonationService donationService) : Controller
{
    [Authorize(Roles = AppRoles.Donor)]
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new CreateDonationViewModel();
        await PopulateCategoriesAsync(model, cancellationToken);
        return View(model);
    }

    [Authorize(Roles = AppRoles.Donor)]
    [HttpPost]
    public async Task<IActionResult> Create(CreateDonationViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCategoriesAsync(model, cancellationToken);
            return View(model);
        }

        var result = await donationService.CreateAsync(new CreateDonationRequest(
            model.FoodCategoryId,
            model.Title,
            model.Description,
            model.Quantity,
            model.Unit,
            model.PreparedAt,
            model.ExpiresAt,
            model.StorageInstructions,
            model.PickupAddress), cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Could not create donation.");
            await PopulateCategoriesAsync(model, cancellationToken);
            return View(model);
        }

        TempData["SuccessMessage"] = "Donation created as a draft.";
        return RedirectToAction(nameof(Mine));
    }

    [Authorize(Roles = AppRoles.Donor)]
    [HttpGet]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken)
        => View(await donationService.GetMineAsync(cancellationToken));

    [Authorize(Roles = AppRoles.Donor)]
    [HttpPost]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        var result = await donationService.PublishAsync(id, cancellationToken);
        if (result.Succeeded)
            TempData["SuccessMessage"] = "Donation published and is now available in the marketplace.";
        else
            TempData["ErrorMessage"] = result.Error ?? "Could not publish donation.";

        return RedirectToAction(nameof(Mine));
    }

    [Authorize(Roles = AppRoles.Beneficiary)]
    [HttpGet]
    public async Task<IActionResult> Available(int page = 1, CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        ViewBag.Page = page;
        return View(await donationService.GetAvailableAsync(page, 20, cancellationToken));
    }

    private async Task PopulateCategoriesAsync(CreateDonationViewModel model, CancellationToken cancellationToken)
    {
        model.Categories = (await donationService.GetCategoriesAsync(cancellationToken))
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToList();
    }
}
