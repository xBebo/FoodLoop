using System.ComponentModel.DataAnnotations;
using FoodLoop.Application.Donations;
using FoodLoop.Application.Identity;
using FoodLoop.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodLoop.Web.Controllers.Api;

public sealed record DonationRequest(
    [Required] Guid? CategoryId,
    [Required] string? Title,
    string? Description,
    [Required] decimal? Quantity,
    [Required] QuantityUnit? Unit,
    [Required] DateTimeOffset? PreparedAt,
    [Required] DateTimeOffset? ExpiresAt,
    string? StorageInstructions,
    [Required] string? PickupAddress,
    // Edit only: the opaque concurrency value from GET /api/donations/{id}/edit.
    string? Version = null);

public sealed record DonationItemResponse(
    Guid Id, string Title, CategoryResponse Category, decimal Quantity, QuantityUnit Unit, DateTimeOffset PreparedAtUtc,
    DateTimeOffset ExpiresAtUtc, string PickupAddress, DonationStatus Status, bool CanEdit, bool CanPublish);

public sealed record DonationDetailsResponse(
    Guid Id, string Title, string Description, CategoryResponse Category, decimal Quantity, QuantityUnit Unit, DateTimeOffset PreparedAtUtc,
    DateTimeOffset ExpiresAtUtc, string StorageInstructions, string PickupAddress, DonationStatus Status, bool CanEdit, bool CanPublish);

public sealed record DonationEditResponse(
    Guid Id, Guid CategoryId, string Title, string Description, decimal Quantity, QuantityUnit Unit, DateTimeOffset PreparedAtUtc,
    DateTimeOffset ExpiresAtUtc, string StorageInstructions, string PickupAddress, string Version);

// Donor donations. Organization, ownership, state and concurrency rules live in DonationService; this only maps results.
// A foreign donation is the same 404 as a missing one. Unsafe methods are covered by the global antiforgery filter.
[ApiController]
[Route("api/donations")]
[Authorize(Roles = AppRoles.Donor)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DonationsApiController(DonationService donations) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<DonationItemResponse>> List(DonationStatus? status, CancellationToken ct) =>
        (await donations.GetMineAsync(status, ct)).Select(x => new DonationItemResponse(
            x.Id, x.Title, new(x.CategoryId, x.Category), x.Quantity, x.Unit, x.PreparedAtUtc, x.ExpiresAtUtc, x.PickupAddress, x.Status,
            DonationService.CanEdit(x.Status), donations.CanPublish(x.Status, x.ExpiresAtUtc)));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var x = await donations.GetDetailsAsync(id, ct);
        return x is null ? NotFoundProblem() : Ok(new DonationDetailsResponse(
            x.Id, x.Title, x.Description, new(x.CategoryId, x.Category), x.Quantity, x.Unit, x.PreparedAtUtc, x.ExpiresAtUtc,
            x.StorageInstructions, x.PickupAddress, x.Status, DonationService.CanEdit(x.Status), donations.CanPublish(x.Status, x.ExpiresAtUtc)));
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var result = await donations.GetForEditAsync(id, ct);
        return result.Outcome switch
        {
            GetDonationForEditOutcome.Success when result.Donation is { } x => Ok(new DonationEditResponse(
                x.Id, x.FoodCategoryId, x.Title, x.Description, x.Quantity, x.Unit, x.PreparedAtUtc, x.ExpiresAtUtc,
                x.StorageInstructions, x.PickupAddress, x.RowVersion)),
            GetDonationForEditOutcome.NotDraft => this.Fail(StatusCodes.Status409Conflict, "donation.not_editable"),
            // Forbidden covers both "not your donation" and "organization not active": neither reveals the donation.
            _ => NotFoundProblem()
        };
    }

    [HttpPost]
    public async Task<IActionResult> Create(DonationRequest request, CancellationToken ct)
    {
        var result = await donations.CreateAsync(new(request.CategoryId!.Value, request.Title!, request.Description ?? string.Empty,
            request.Quantity!.Value, request.Unit!.Value, request.PreparedAt!.Value, request.ExpiresAt!.Value,
            request.StorageInstructions ?? string.Empty, request.PickupAddress!), ct);
        if (result.Succeeded) return Created($"/api/donations/{result.DonationId}", new { id = result.DonationId });
        // Creating has no donation to hide yet, so Forbidden is the organization/account state.
        return result.Failure == DonationFailureKind.Forbidden
            ? this.Fail(StatusCodes.Status403Forbidden, "organization.not_active")
            : Failure(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, DonationRequest request, CancellationToken ct)
    {
        var result = await donations.UpdateAsync(id, new(request.CategoryId!.Value, request.Title!, request.Description ?? string.Empty,
            request.Quantity!.Value, request.Unit!.Value, request.PreparedAt!.Value, request.ExpiresAt!.Value,
            request.StorageInstructions ?? string.Empty, request.PickupAddress!, request.Version ?? string.Empty), ct);
        return result.Succeeded ? NoContent() : Failure(result);
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        var result = await donations.PublishAsync(id, ct);
        return result.Succeeded ? NoContent() : Failure(result);
    }

    private IActionResult Failure(DonationOperationResult result) => result.Failure switch
    {
        DonationFailureKind.Validation => this.Fail(StatusCodes.Status400BadRequest, "donation.invalid", result.Error),
        DonationFailureKind.InvalidState => this.Fail(StatusCodes.Status409Conflict, "donation.invalid_state", result.Error),
        DonationFailureKind.Conflict => this.Fail(StatusCodes.Status409Conflict, "donation.stale"),
        _ => NotFoundProblem()
    };

    private ObjectResult NotFoundProblem() => this.Fail(StatusCodes.Status404NotFound, "donation.not_found");
}
