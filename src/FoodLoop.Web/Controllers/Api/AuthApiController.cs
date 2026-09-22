using System.ComponentModel.DataAnnotations;
using FoodLoop.Application.Identity;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodLoop.Web.Controllers.Api;

public sealed record LoginRequest([Required] string? Email, [Required] string? Password);

public sealed record RegisterRequest(
    [Required, MaxLength(200)] string? OrganizationName,
    [Required, MaxLength(100)] string? LicenseNumber,
    [Required] OrganizationType? OrganizationType,
    [Required, EmailAddress, MaxLength(256)] string? Email,
    [Required] string? Password);

public sealed record SessionResponse(bool IsAuthenticated, string DisplayName, IReadOnlyList<string> Roles, SessionOrganization? Organization);

// Session endpoints for the SPA. Same Identity cookie and rules as the MVC AuthController; unsafe methods need X-XSRF-TOKEN.
[ApiController]
[Route("api/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthApiController(AccountService accounts, IAntiforgery antiforgery, ILogger<AuthApiController> logger) : ControllerBase
{
    // The token is bound to the current identity, so the SPA fetches a fresh one after every login and logout.
    [HttpGet("antiforgery"), AllowAnonymous]
    public IActionResult Antiforgery() => Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });

    [HttpGet("session"), AllowAnonymous]
    public async Task<IActionResult> Session(CancellationToken ct)
    {
        var session = User.Identity?.IsAuthenticated == true ? await accounts.GetSessionAsync(User, ct) : null;
        return Ok(session is null ? new { isAuthenticated = false } : ToResponse(session));
    }

    [HttpPost("login"), AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await accounts.SignInAsync(request.Email, request.Password, ct);
        return result.Outcome switch
        {
            LoginOutcome.Succeeded => Ok(ToResponse(result.Session!)),
            LoginOutcome.AccountUnavailable => this.Fail(StatusCodes.Status403Forbidden, "auth.account_unavailable"),
            _ => this.Fail(StatusCodes.Status401Unauthorized, "auth.invalid_credentials")
        };
    }

    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout()
    {
        await accounts.SignOutAsync();
        return NoContent();
    }

    [HttpPost("register"), AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await accounts.RegisterAsync(
            new(request.OrganizationName, request.LicenseNumber, request.OrganizationType!.Value, request.Email), request.Password, ct);
        switch (result.Outcome)
        {
            case RegistrationOutcome.Succeeded: return StatusCode(StatusCodes.Status201Created);
            case RegistrationOutcome.DuplicateAccount: return this.Fail(StatusCodes.Status409Conflict, "auth.account_exists");
            case RegistrationOutcome.DuplicateLicense: return this.Fail(StatusCodes.Status409Conflict, "organization.license_exists");
            case RegistrationOutcome.InvalidOrganizationType: return this.Fail(StatusCodes.Status400BadRequest, "organization.invalid_type");
            case RegistrationOutcome.InvalidInput: return this.Fail(StatusCodes.Status400BadRequest, "validation");
            case RegistrationOutcome.IdentityFailed:
                // Identity's descriptions are safe user-facing text; its codes decide which field they belong to.
                foreach (var error in result.Errors)
                    ModelState.AddModelError(error.Code.StartsWith("Password", StringComparison.Ordinal) ? "password" : "email", error.Description);
                return ValidationProblem(ModelState);
            default:
                logger.LogError("Registration failed with {Outcome}.", result.Outcome);
                return this.Fail(StatusCodes.Status500InternalServerError, "server.error");
        }
    }

    private static SessionResponse ToResponse(AccountSession session) => new(true, session.DisplayName, session.Roles, session.Organization);
}
