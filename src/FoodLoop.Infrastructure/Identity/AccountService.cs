using System.Security.Claims;
using FoodLoop.Application.Identity;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Infrastructure.Identity;

// Shared login and registration rules for every sign-in surface. Passwords are only handed to Identity.
public sealed class AccountService(
    ApplicationDbContext db,
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole<Guid>> roles,
    SignInManager<ApplicationUser> signIn)
{
    public async Task<LoginResult> SignInAsync(string? email, string? password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password)) return new(LoginOutcome.InvalidCredentials);

        var normalized = users.NormalizeEmail(email.Trim());
        var user = await db.Users.Include(u => u.Organization).FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, ct);
        if (user is null)
        {
            // Spend the same hashing work as a real check so response time does not reveal unknown accounts.
            users.PasswordHasher.HashPassword(new ApplicationUser(), password);
            return new(LoginOutcome.InvalidCredentials);
        }

        // Password proof first, without issuing a cookie; only then may the account's eligibility be revealed.
        if (!(await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: false)).Succeeded)
            return new(LoginOutcome.InvalidCredentials);
        if (!LoginEligibility.CanSignIn(user.Organization?.Type, user.Organization?.Status))
            return new(LoginOutcome.AccountUnavailable);

        await signIn.SignInAsync(user, isPersistent: false);
        return new(LoginOutcome.Succeeded, await SessionOf(user));
    }

    // Reads the account fresh from the database, so the session reflects the current organization status.
    public async Task<AccountSession?> GetSessionAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        if (!Guid.TryParse(users.GetUserId(principal), out var id)) return null;
        var user = await db.Users.Include(u => u.Organization).FirstOrDefaultAsync(u => u.Id == id, ct);
        return user is null ? null : await SessionOf(user);
    }

    private async Task<AccountSession> SessionOf(ApplicationUser user)
    {
        var org = user.Organization;
        var displayName = !string.IsNullOrWhiteSpace(user.DisplayName) ? user.DisplayName : org?.Name ?? "FoodLoop member";
        return new(displayName, [.. await users.GetRolesAsync(user)], org is null ? null : new(org.Name, org.Type, org.Status));
    }

    public Task SignOutAsync() => signIn.SignOutAsync();

    public async Task<RegistrationResult> RegisterAsync(
        RegisterOrganizationRequest request,
        string? password,
        CancellationToken ct = default)
    {
        if (!Enum.IsDefined(request.OrganizationType)) return new(RegistrationOutcome.InvalidOrganizationType);
        if (string.IsNullOrWhiteSpace(request.OrganizationName) || request.OrganizationName.Length > 200 ||
            string.IsNullOrWhiteSpace(request.LicenseNumber) || request.LicenseNumber.Length > 100 ||
            password is null)
            return new(RegistrationOutcome.InvalidInput);

        // Uncommitted on every early return, so a failed step never leaves an orphan organization.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            Organization = new Organization
            {
                Name = request.OrganizationName,
                LicenseNumber = request.LicenseNumber,
                Type = request.OrganizationType,
                Status = OrganizationStatus.Pending
            }
        };

        IdentityResult created;
        try { created = await users.CreateAsync(user, password); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 } sql)
        {
            // The only unique value Identity does not pre-validate is the license; anything else stays a duplicate account.
            return new(sql.Message.Contains("IX_Organizations_LicenseNumber", StringComparison.Ordinal)
                ? RegistrationOutcome.DuplicateLicense
                : RegistrationOutcome.DuplicateAccount);
        }
        if (!created.Succeeded)
        {
            var duplicate = created.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName");
            return new(duplicate ? RegistrationOutcome.DuplicateAccount : RegistrationOutcome.IdentityFailed,
                created.Errors.Select(e => new RegistrationError(e.Code, e.Description)).ToList());
        }

        var role = request.OrganizationType == OrganizationType.Donor ? AppRoles.Donor : AppRoles.Beneficiary;
        if (!await roles.RoleExistsAsync(role)) return new(RegistrationOutcome.RolesNotConfigured);
        if (!(await users.AddToRoleAsync(user, role)).Succeeded) return new(RegistrationOutcome.RoleAssignmentFailed);

        await transaction.CommitAsync(ct);
        return new(RegistrationOutcome.Succeeded);
    }
}
