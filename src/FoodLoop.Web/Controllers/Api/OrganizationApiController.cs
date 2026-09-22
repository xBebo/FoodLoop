using FoodLoop.Application.Organizations;
using FoodLoop.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodLoop.Web.Controllers.Api;

public sealed record OrganizationResponse(
    string Name, string Address, string LicenseNumber, OrganizationType Type, OrganizationStatus Status, DateTimeOffset CreatedAt, bool IsReadOnly);

// The signed-in member's own organization, read-only. Visibility rules live in OrganizationProfileService.
[ApiController]
[Route("api/organization")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OrganizationApiController(OrganizationProfileService profiles) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var org = await profiles.GetMyOrganizationAsync(ct);
        return org is null
            ? this.Fail(StatusCodes.Status404NotFound, "organization.not_found")
            : Ok(new OrganizationResponse(org.Name, org.Address, org.LicenseNumber, org.Type, org.Status, org.CreatedAtUtc,
                org.Status == OrganizationStatus.Suspended));
    }
}
