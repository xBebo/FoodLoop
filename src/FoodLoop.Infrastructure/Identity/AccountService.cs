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
    public async Task<LoginOutcome> SignInAsync(string? email, string? password, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(email) || password is null) return LoginOutcome.UnknownAccount;

        var user = await db.Users.Include(u => u.Organization).FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null) return LoginOutcome.UnknownAccount;

        // Kept in the original order (status before password) so MVC messages are unchanged; see R6.2 note.
        if (!LoginEligibility.CanSignIn(user.Organization?.Type, user.Organization?.Status))
            return LoginOutcome.OrganizationNotActive;

        var result = await signIn.PasswordSignInAsync(user.UserName!, password, isPersistent: false, lockoutOnFailure: false);
        return result.Succeeded ? LoginOutcome.Succeeded : LoginOutcome.InvalidPassword;
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
                created.Errors.Select(e => e.Description).ToList());
        }

        var role = request.OrganizationType == OrganizationType.Donor ? AppRoles.Donor : AppRoles.Beneficiary;
        if (!await roles.RoleExistsAsync(role)) return new(RegistrationOutcome.RolesNotConfigured);
        if (!(await users.AddToRoleAsync(user, role)).Succeeded) return new(RegistrationOutcome.RoleAssignmentFailed);

        await transaction.CommitAsync(ct);
        return new(RegistrationOutcome.Succeeded);
    }
}
