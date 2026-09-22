using System.ComponentModel.DataAnnotations;
using FoodLoop.Application.Claims;
using FoodLoop.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodLoop.Web.Controllers.Api;

public sealed record CreateClaimRequest([Required] Guid? DonationId);

public sealed record CreateClaimResponse(Guid ClaimId);

// Claim rules, organization checks and double-booking protection live in ClaimService; this only maps outcomes.
// POST is protected by the global antiforgery filter (X-XSRF-TOKEN).
[ApiController]
[Route("api/claims")]
[Authorize(Roles = AppRoles.Beneficiary)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ClaimsApiController(ClaimService claims, ILogger<ClaimsApiController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateClaimRequest request, CancellationToken ct)
    {
        var result = await claims.CreateAsync(request.DonationId!.Value, ct);
        switch (result.Outcome)
        {
            case CreateClaimOutcome.Created: return Created($"/api/claims/{result.ClaimId}", new CreateClaimResponse(result.ClaimId!.Value));
            case CreateClaimOutcome.Unauthenticated: return this.Fail(StatusCodes.Status401Unauthorized, "auth.unauthenticated");
            case CreateClaimOutcome.Forbidden: return this.Fail(StatusCodes.Status403Forbidden, "auth.forbidden");
            case CreateClaimOutcome.OrganizationNotActive: return this.Fail(StatusCodes.Status403Forbidden, "organization.not_active");
            case CreateClaimOutcome.DonationNotFound: return this.Fail(StatusCodes.Status404NotFound, "donation.not_found");
            // An inactive donor is reported like any other unavailable donation, so nothing about the donor leaks.
            case CreateClaimOutcome.DonationNotAvailable or CreateClaimOutcome.DonorOrganizationNotActive:
                return this.Fail(StatusCodes.Status409Conflict, "claim.not_available");
            case CreateClaimOutcome.DonationExpired: return this.Fail(StatusCodes.Status409Conflict, "claim.expired");
            case CreateClaimOutcome.Conflict: return this.Fail(StatusCodes.Status409Conflict, "claim.conflict");
            default:
                logger.LogError("Claim creation returned unhandled outcome {Outcome}.", result.Outcome);
                return this.Fail(StatusCodes.Status500InternalServerError, "server.error");
        }
    }

    // ClaimSummary / ClaimDetails are application read models (no entities, ids of other parties or audit data).
    [HttpGet]
    public async Task<IActionResult> List(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await claims.GetMyClaimsAsync(page, pageSize, ct);
        return result.Outcome == GetMyClaimsOutcome.Success
            ? Ok(new { items = result.Claims, page, hasNext = result.HasNext })
            : this.Fail(StatusCodes.Status403Forbidden, "auth.forbidden");
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var result = await claims.GetDetailsAsync(id, ct);
        return result.Outcome switch
        {
            GetClaimDetailsOutcome.Success => Ok(result.Details),
            GetClaimDetailsOutcome.OrganizationNotActive => this.Fail(StatusCodes.Status403Forbidden, "organization.not_active"),
            GetClaimDetailsOutcome.NotFound => this.Fail(StatusCodes.Status404NotFound, "claim.not_found"),
            _ => this.Fail(StatusCodes.Status403Forbidden, "auth.forbidden")
        };
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var result = await claims.CancelAsync(id, ct);
        switch (result.Outcome)
        {
            case CancelClaimOutcome.Cancelled: return NoContent();
            case CancelClaimOutcome.OrganizationNotActive: return this.Fail(StatusCodes.Status403Forbidden, "organization.not_active");
            case CancelClaimOutcome.ClaimNotFound: return this.Fail(StatusCodes.Status404NotFound, "claim.not_found");
            case CancelClaimOutcome.NotCancellable: return this.Fail(StatusCodes.Status409Conflict, "claim.not_cancellable");
            case CancelClaimOutcome.Conflict: return this.Fail(StatusCodes.Status409Conflict, "claim.conflict");
            case CancelClaimOutcome.Unauthenticated: return this.Fail(StatusCodes.Status401Unauthorized, "auth.unauthenticated");
            case CancelClaimOutcome.Forbidden: return this.Fail(StatusCodes.Status403Forbidden, "auth.forbidden");
            default:
                logger.LogError("Claim cancellation returned unhandled outcome {Outcome}.", result.Outcome);
                return this.Fail(StatusCodes.Status500InternalServerError, "server.error");
        }
    }
}
