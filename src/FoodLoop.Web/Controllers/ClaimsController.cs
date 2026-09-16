using FoodLoop.Application.Claims;
using FoodLoop.Web.Models;
using Microsoft.AspNetCore.Mvc;
namespace FoodLoop.Web.Controllers;
// Authorization and business rules live in ClaimService; this controller only maps outcomes to MVC results.
// POST is protected by the global AutoValidateAntiforgeryTokenAttribute registered in Program.cs.
public sealed class ClaimsController(ClaimService claims) : Controller
{
    // donationId is the only client-supplied value; the beneficiary organization always comes from the signed-in user.
    [HttpPost]
    public async Task<IActionResult> Create(Guid donationId, CancellationToken ct)
    {
        var result = await claims.CreateAsync(donationId, ct);
        return result.Outcome switch
        {
            CreateClaimOutcome.Created => RedirectWith("Success", "Donation claimed."),
            CreateClaimOutcome.Unauthenticated => Challenge(),
            CreateClaimOutcome.Forbidden or CreateClaimOutcome.OrganizationNotActive => Forbid(),
            CreateClaimOutcome.DonationNotFound => NotFound(),
            CreateClaimOutcome.DonorOrganizationNotActive => RedirectWith("Error", "This donation cannot be claimed right now."),
            CreateClaimOutcome.DonationNotAvailable => RedirectWith("Error", "This donation is no longer available."),
            CreateClaimOutcome.DonationExpired => RedirectWith("Error", "This donation has expired."),
            CreateClaimOutcome.Conflict => RedirectWith("Error", "This donation was just claimed by someone else."),
            _ => throw new InvalidOperationException($"Unhandled claim outcome {result.Outcome}.")
        };
    }

    // Out-of-range paging is normalized (page >= 1, pageSize 1..100) so ordinary bad query strings never hit the repository guard.
    [HttpGet]
    public async Task<IActionResult> Mine(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await claims.GetMyClaimsAsync(page, pageSize, ct);
        return result.Outcome switch
        {
            GetMyClaimsOutcome.Success => View(new MyClaimsViewModel(result.Claims, page, pageSize)),
            GetMyClaimsOutcome.Unauthenticated => Challenge(),
            GetMyClaimsOutcome.Forbidden or GetMyClaimsOutcome.OrganizationNotBeneficiary => Forbid(),
            _ => throw new InvalidOperationException($"Unhandled my-claims outcome {result.Outcome}.")
        };
    }

    private RedirectToActionResult RedirectWith(string tempDataKey, string message)
    {
        TempData[tempDataKey] = message;
        return RedirectToAction(nameof(Mine));
    }
}
