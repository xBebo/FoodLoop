using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.Organizations;

public sealed record OrganizationAdminItem(
    Guid Id,
    string Name,
    OrganizationType Type,
    string LicenseNumber,
    OrganizationStatus Status);

public sealed record OrganizationAdminPage(
    IReadOnlyList<OrganizationAdminItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    OrganizationStatus? Status)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public sealed record PendingOrganizationItem(
    Guid Id,
    string Name,
    OrganizationType Type,
    string LicenseNumber,
    DateTimeOffset CreatedAtUtc);

public enum OrganizationStatusChangeOutcome
{
    Updated,
    NotFound,
    InvalidState,
    Conflict
}

public sealed record OrganizationStatusChangeResult(OrganizationStatusChangeOutcome Outcome, string? Error = null)
{
    public bool Succeeded => Outcome == OrganizationStatusChangeOutcome.Updated;
}
