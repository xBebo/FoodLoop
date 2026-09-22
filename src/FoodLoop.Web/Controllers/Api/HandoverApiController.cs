using System.ComponentModel.DataAnnotations;
using FoodLoop.Application.Courier;
using FoodLoop.Application.Identity;
using FoodLoop.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;

namespace FoodLoop.Web.Controllers.Api;

public sealed record IssueCodeRequest([Required] HandoverType? Type);

public sealed record IssuedCodeResponse(string Code, HandoverType Type, DateTimeOffset ExpiresAtUtc, string QrSvg);

// Donor (pickup) and beneficiary (delivery) handover codes. The raw code exists only in this response: the database keeps a
// hash, nothing can read it back, and issuing again revokes the previous one (CourierService.IssueAsync). Never logged.
[ApiController]
[Route("api/handover")]
[Authorize(Roles = AppRoles.Donor + "," + AppRoles.Beneficiary)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class HandoverApiController(CourierService couriers) : ControllerBase
{
    [HttpGet("tasks")]
    public async Task<IActionResult> Tasks(CancellationToken ct)
    {
        try
        {
            return Ok((await couriers.OrganizationTasksAsync(ct)).Select(x => new { x.ClaimId, x.DonationTitle, x.Status, type = x.IssueType, x.CanIssue }));
        }
        catch (UnauthorizedAccessException) { return this.Fail(StatusCodes.Status403Forbidden, "organization.not_active"); }
    }

    [HttpPost("{claimId:guid}/codes")]
    public async Task<IActionResult> Issue(Guid claimId, IssueCodeRequest request, CancellationToken ct)
    {
        CourierResult result;
        // Not this organization's claim, or not this role's handover type: the same 404 as a missing claim.
        try { result = await couriers.IssueAsync(claimId, request.Type!.Value, ct); }
        catch (UnauthorizedAccessException) { return this.Fail(StatusCodes.Status404NotFound, "claim.not_found"); }
        if (!result.Succeeded) return result.Failure switch
        {
            CourierFailureKind.InvalidState => this.Fail(StatusCodes.Status409Conflict, "handover.not_ready"),
            CourierFailureKind.Conflict => this.Fail(StatusCodes.Status409Conflict, "claim.conflict"),
            CourierFailureKind.Validation => this.Fail(StatusCodes.Status400BadRequest, "validation"),
            _ => this.Fail(StatusCodes.Status404NotFound, "claim.not_found")
        };
        using var qrData = QRCodeGenerator.GenerateQrCode(result.Token!, QRCodeGenerator.ECCLevel.M);
        using var qr = new SvgQRCode(qrData);
        return Ok(new IssuedCodeResponse(result.Token!, request.Type!.Value, result.ExpiresAtUtc!.Value, qr.GetGraphic(8)));
    }
}
