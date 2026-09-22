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
            // No Location header yet: GET /api/claims/{id} arrives with My Claims.
            case CreateClaimOutcome.Created: return StatusCode(StatusCodes.Status201Created, new CreateClaimResponse(result.ClaimId!.Value));
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
}
