using System.Security.Cryptography;
using System.Text;
using FoodLoop.Application.Donations;
using Microsoft.AspNetCore.Mvc;

namespace FoodLoop.Web.Controllers.Api;

[ApiController]
[Route("api/internal/maintenance")]
public sealed class MaintenanceController(
    IDonationExpiryService expiry,
    IConfiguration configuration,
    ILogger<MaintenanceController> logger) : ControllerBase
{
    [HttpGet("expire-donations")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> ExpireDonations(CancellationToken cancellationToken)
    {
        var configuredSecret = configuration["CRON_SECRET"];
        var suppliedAuthorization = Request.Headers.Authorization.ToString();

        if (!IsAuthorized(configuredSecret, suppliedAuthorization))
            return Unauthorized();

        var result = await expiry.ExpireDueAsync(cancellationToken);
        if (!result.Succeeded)
        {
            logger.LogWarning("Scheduled donation expiry encountered a concurrent state change.");
            return Conflict(new { success = false, code = "expiry.concurrent_change" });
        }

        return Ok(new { success = true, expiredCount = result.ExpiredCount });
    }

    private static bool IsAuthorized(string? secret, string authorization)
    {
        if (string.IsNullOrWhiteSpace(secret)) return false;

        var expected = Encoding.UTF8.GetBytes($"Bearer {secret}");
        var supplied = Encoding.UTF8.GetBytes(authorization);
        return expected.Length == supplied.Length &&
               CryptographicOperations.FixedTimeEquals(expected, supplied);
    }
}
