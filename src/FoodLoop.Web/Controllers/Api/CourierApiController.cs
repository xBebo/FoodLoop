using System.ComponentModel.DataAnnotations;
using FoodLoop.Application.Courier;
using FoodLoop.Application.Identity;
using FoodLoop.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodLoop.Web.Controllers.Api;

public sealed record VerifyHandoverRequest([Required] HandoverType? Type, [Required] string? Code);

public sealed record HandoverEvidenceResponse(HandoverType Type, DateTimeOffset VerifiedAtUtc);

// No courier/user ids, notes or token data: only what the courier needs to travel and verify.
public sealed record CourierTaskDetailsResponse(
    Guid ClaimId, string DonationTitle, string DonorName, string BeneficiaryName, string PickupAddress, DateTimeOffset ExpiresAtUtc,
    ClaimStatus Status, CourierNextStep NextStep, IReadOnlyList<HandoverEvidenceResponse> Evidence);

// Courier tasks. The assigned-courier check, lifecycle and token rules live in CourierService / TaskDetailsService.
// Another courier's task is the same 404 as a missing one.
[ApiController]
[Route("api/courier/tasks")]
[Authorize(Roles = AppRoles.Courier)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CourierApiController(CourierService couriers, TaskDetailsService details) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<CourierTaskItem>> Tasks(CancellationToken ct) => couriers.MyTasksAsync(ct);

    [HttpGet("{claimId:guid}")]
    public async Task<IActionResult> Details(Guid claimId, CancellationToken ct)
    {
        var x = await details.GetTaskDetailsAsync(claimId, ct);
        if (x is null) return this.Fail(StatusCodes.Status404NotFound, "task.not_found");
        var evidence = new List<HandoverEvidenceResponse>();
        if (x.PickupHandoverEvidence is { } pickup) evidence.Add(new(HandoverType.Pickup, pickup.CompletedAtUtc));
        if (x.DeliveryHandoverEvidence is { } delivery) evidence.Add(new(HandoverType.Delivery, delivery.CompletedAtUtc));
        return Ok(new CourierTaskDetailsResponse(x.ClaimId, x.DonationTitle, x.DonorOrganizationName, x.BeneficiaryOrganizationName,
            x.PickupAddress, x.ExpiryDate, x.Status, x.NextStepKind, evidence));
    }

    [HttpPost("{claimId:guid}/verify")]
    public async Task<IActionResult> Verify(Guid claimId, VerifyHandoverRequest request, CancellationToken ct)
    {
        // Codes are often pasted with spaces or line breaks; whitespace is never part of a code.
        var code = string.Concat(request.Code!.Where(c => !char.IsWhiteSpace(c)));
        CourierResult result;
        try { result = await couriers.VerifyAsync(claimId, code, request.Type!.Value, ct); }
        catch (UnauthorizedAccessException) { return this.Fail(StatusCodes.Status404NotFound, "task.not_found"); }
        return result.Succeeded ? NoContent() : result.Failure switch
        {
            CourierFailureKind.Validation => this.Fail(StatusCodes.Status400BadRequest, "handover.invalid_code"),
            CourierFailureKind.InvalidState => this.Fail(StatusCodes.Status409Conflict, "handover.invalid_state"),
            CourierFailureKind.Conflict => this.Fail(StatusCodes.Status409Conflict, "task.conflict"),
            _ => this.Fail(StatusCodes.Status404NotFound, "task.not_found")
        };
    }
}
