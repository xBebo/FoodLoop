using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.Identity;

public enum LoginOutcome
{
    Succeeded,
    UnknownAccount,
    OrganizationNotActive,
    InvalidPassword
}

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

public sealed record RegistrationResult(RegistrationOutcome Outcome, IReadOnlyList<string>? Errors = null)
{
    public bool Succeeded => Outcome == RegistrationOutcome.Succeeded;
    public IReadOnlyList<string> Errors { get; } = Errors ?? [];
}
