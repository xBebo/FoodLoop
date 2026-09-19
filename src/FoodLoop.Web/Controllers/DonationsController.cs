using FoodLoop.Application.Donations;
using FoodLoop.Application.Identity;
using FoodLoop.Domain.Enums;
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
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await donationService.GetForEditAsync(id, cancellationToken);
        if (result.Outcome == GetDonationForEditOutcome.NotFound) return NotFound();
        if (result.Outcome == GetDonationForEditOutcome.Forbidden) return Forbid();
        if (result.Outcome == GetDonationForEditOutcome.NotDraft)
        {
            TempData["ErrorMessage"] = "Only draft donations can be edited.";
            return RedirectToAction(nameof(Mine));
        }

        var donation = result.Donation!;
        var model = new EditDonationViewModel
        {
            Id = donation.Id,
            FoodCategoryId = donation.FoodCategoryId,
            Title = donation.Title,
            Description = donation.Description,
            Quantity = donation.Quantity,
            Unit = donation.Unit,
            PreparedAt = donation.PreparedAtUtc.ToLocalTime(),
            ExpiresAt = donation.ExpiresAtUtc.ToLocalTime(),
            StorageInstructions = donation.StorageInstructions,
            PickupAddress = donation.PickupAddress,
            RowVersion = donation.RowVersion
        };
        await PopulateCategoriesAsync(model, cancellationToken);
        return View(model);
    }

    [Authorize(Roles = AppRoles.Donor)]
    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, EditDonationViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid)
        {
            await PopulateCategoriesAsync(model, cancellationToken);
            return View(model);
        }

        var result = await donationService.UpdateAsync(id, new UpdateDonationRequest(
            model.FoodCategoryId,
            model.Title,
            model.Description,
            model.Quantity,
            model.Unit,
            model.PreparedAt,
            model.ExpiresAt,
            model.StorageInstructions,
            model.PickupAddress,
            model.RowVersion), cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Could not update donation.");
            await PopulateCategoriesAsync(model, cancellationToken);
            return View(model);
        }

        TempData["SuccessMessage"] = "Draft donation updated.";
        return RedirectToAction(nameof(Mine));
    }

    [Authorize(Roles = AppRoles.Donor)]
    [HttpGet]
    public async Task<IActionResult> Mine(DonationStatus? status, CancellationToken cancellationToken)
    {
        if (status is DonationStatus selectedStatus && !Enum.IsDefined(selectedStatus)) status = null;
        var items = await donationService.GetMineAsync(status, cancellationToken);
        return View(new MyDonationsViewModel(items, status));
    }

    [Authorize(Roles = AppRoles.Donor)]
    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var donation = await donationService.GetDetailsAsync(id, cancellationToken);
        return donation is null ? NotFound() : View(donation);
    }

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
    public async Task<IActionResult> Available(
        string? search,
        Guid? categoryId,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        var result = await donationService.GetAvailableAsync(search, categoryId, page, cancellationToken);
        var categories = (await donationService.GetCategoriesAsync(cancellationToken))
            .Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == categoryId))
            .ToList();
        return View(new AvailableDonationsViewModel(
            result.Items,
            categories,
            result.Search,
            result.CategoryId,
            result.Page,
            result.HasPrevious,
            result.HasNext));
    }

    private async Task PopulateCategoriesAsync(CreateDonationViewModel model, CancellationToken cancellationToken)
    {
        model.Categories = (await donationService.GetCategoriesAsync(cancellationToken))
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToList();
    }

    private async Task PopulateCategoriesAsync(EditDonationViewModel model, CancellationToken cancellationToken)
    {
        model.Categories = (await donationService.GetCategoriesAsync(cancellationToken))
            .Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == model.FoodCategoryId))
            .ToList();
    }
}
