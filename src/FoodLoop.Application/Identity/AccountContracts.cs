using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.Identity;

// Unknown account and wrong password are one outcome, and eligibility is only reported after the password
// is proven, so a caller without the password learns nothing about the account or its organization.
public enum LoginOutcome
{
    Succeeded,
    InvalidCredentials,
    AccountUnavailable
}

// What a signed-in caller may learn about their own account. No ids, email or security data.
public sealed record SessionOrganization(string Name, OrganizationType Type, OrganizationStatus Status);
public sealed record AccountSession(string DisplayName, IReadOnlyList<string> Roles, SessionOrganization? Organization);
public sealed record LoginResult(LoginOutcome Outcome, AccountSession? Session = null);

public static class LoginEligibility
{
    // Accounts without an organization (Admin, Courier) are not gated here. Suspended beneficiaries keep
    // read-only access to their claims; every other non-active organization is blocked from signing in.
    public static bool CanSignIn(OrganizationType? organizationType, OrganizationStatus? organizationStatus) => organizationStatus switch
    {
        null or OrganizationStatus.Active => true,
        OrganizationStatus.Suspended => organizationType == OrganizationType.Beneficiary,
        _ => false
    };
}

// The password is passed separately so it never travels inside a printable record.
public sealed record RegisterOrganizationRequest(
    string? OrganizationName,
    string? LicenseNumber,
    OrganizationType OrganizationType,
    string? Email);

public enum RegistrationOutcome
{
    Succeeded,
    InvalidInput,
    InvalidOrganizationType,
    IdentityFailed,
    DuplicateAccount,
    DuplicateLicense,
    RolesNotConfigured,
    RoleAssignmentFailed
}

// Code is Identity's stable error code (e.g. PasswordTooShort); Description is its safe, user-facing text.
public sealed record RegistrationError(string Code, string Description);

public sealed record RegistrationResult(RegistrationOutcome Outcome, IReadOnlyList<RegistrationError>? Errors = null)
{
    public bool Succeeded => Outcome == RegistrationOutcome.Succeeded;
    public IReadOnlyList<RegistrationError> Errors { get; } = Errors ?? [];
}
