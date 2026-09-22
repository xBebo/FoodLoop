using FoodLoop.Application.Donations;
using FoodLoop.Application.Identity;
using FoodLoop.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodLoop.Web.Controllers.Api;

public sealed record CategoryResponse(Guid Id, string Name);

public sealed record MarketplaceItemResponse(
    Guid Id, string Title, CategoryResponse Category, decimal Quantity, QuantityUnit Unit,
    DateTimeOffset PreparedAtUtc, DateTimeOffset ExpiresAtUtc, DonationStatus Status, string PickupAddress, string DonorName);

public sealed record MarketplacePageResponse(IReadOnlyList<MarketplaceItemResponse> Items, int Page, bool HasPrevious, bool HasNext);

public sealed record MarketplaceDetailsResponse(
    Guid Id, string Title, string Description, CategoryResponse Category, decimal Quantity, QuantityUnit Unit,
    DateTimeOffset PreparedAtUtc, DateTimeOffset ExpiresAtUtc, string StorageInstructions, string PickupAddress, string DonorName);

// Beneficiary marketplace reads. Visibility and organization rules live in DonationService; this only maps results.
[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MarketplaceApiController(DonationService donations) : ControllerBase
{
    [HttpGet("api/categories"), Authorize]
    public async Task<IEnumerable<CategoryResponse>> Categories(CancellationToken ct) =>
        (await donations.GetCategoriesAsync(ct)).Select(x => new CategoryResponse(x.Id, x.Name));

    // Search matches the donation title only (the backend's real capability). Paging has no total by design.
    [HttpGet("api/marketplace"), Authorize(Roles = AppRoles.Beneficiary)]
    public async Task<IActionResult> List(string? search, Guid? categoryId, int page = 1, CancellationToken ct = default)
    {
        var result = await donations.GetAvailableAsync(search, categoryId, Math.Max(page, 1), ct);
        if (!result.IsAllowed) return this.Fail(StatusCodes.Status403Forbidden, "organization.not_active");
        return Ok(new MarketplacePageResponse(
            [.. result.Items.Select(x => new MarketplaceItemResponse(x.Id, x.Title, new(x.CategoryId, x.Category), x.Quantity, x.Unit,
                x.PreparedAtUtc, x.ExpiresAtUtc, x.Status, x.PickupAddress, x.DonorName ?? string.Empty))],
            result.Page, result.HasPrevious, result.HasNext));
    }

    [HttpGet("api/marketplace/{id:guid}"), Authorize(Roles = AppRoles.Beneficiary)]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var result = await donations.GetMarketplaceDetailsAsync(id, ct);
        return result.Outcome switch
        {
            GetMarketplaceDonationOutcome.Success when result.Donation is { } x => Ok(new MarketplaceDetailsResponse(
                x.Id, x.Title, x.Description, new(x.Category.Id, x.Category.Name), x.Quantity, x.Unit,
                x.PreparedAtUtc, x.ExpiresAtUtc, x.StorageInstructions, x.PickupAddress, x.DonorName)),
            GetMarketplaceDonationOutcome.Forbidden => this.Fail(StatusCodes.Status403Forbidden, "organization.not_active"),
            _ => this.Fail(StatusCodes.Status404NotFound, "donation.not_found")
        };
    }
}
