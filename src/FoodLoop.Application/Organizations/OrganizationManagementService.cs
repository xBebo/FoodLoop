using FoodLoop.Application.Exceptions;
using FoodLoop.Application.Identity;
using FoodLoop.Application.Interfaces.Auditing;
using FoodLoop.Application.Interfaces.Identity;
using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.Organizations;

public sealed class OrganizationManagementService(
    ICurrentUserService user,
    IRepository<Organization> organizations,
    IOrganizationAdminReadRepository readRepository,
    IAuditService audit,
    IUnitOfWork unitOfWork)
{
    private void RequireAdmin()
    {
        if (!user.IsAuthenticated || !user.IsInRole(AppRoles.Admin))
            throw new UnauthorizedAccessException();
    }

    public Task<OrganizationAdminPage> GetPageAsync(
        OrganizationStatus? status,
        int page = 1,
        CancellationToken ct = default)
    {
        RequireAdmin();
        return readRepository.GetPageAsync(status, Math.Max(1, page), 20, ct);
    }

    public Task<OrganizationStatusChangeResult> SuspendAsync(Guid id, CancellationToken ct = default) =>
        ChangeStatusAsync(id, OrganizationStatus.Active, OrganizationStatus.Suspended, "OrganizationSuspended", ct);

    public Task<OrganizationStatusChangeResult> ReactivateAsync(Guid id, CancellationToken ct = default) =>
        ChangeStatusAsync(id, OrganizationStatus.Suspended, OrganizationStatus.Active, "OrganizationReactivated", ct);

    private async Task<OrganizationStatusChangeResult> ChangeStatusAsync(
        Guid id,
        OrganizationStatus expected,
        OrganizationStatus target,
        string auditAction,
        CancellationToken ct)
    {
        RequireAdmin();
        var organization = await organizations.GetByIdAsync(id, ct);
        if (organization is null)
            return new(OrganizationStatusChangeOutcome.NotFound, "Organization not found.");
        if (organization.Status != expected)
            return new(OrganizationStatusChangeOutcome.InvalidState,
                expected == OrganizationStatus.Active
                    ? "Only active organizations can be suspended."
                    : "Only suspended organizations can be reactivated.");

        organization.Status = target;
        audit.Record(auditAction, nameof(Organization), organization.Id);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
            return new(OrganizationStatusChangeOutcome.Updated);
        }
        catch (PersistenceConflictException)
        {
            return new(OrganizationStatusChangeOutcome.Conflict,
                "Another administrator changed this organization. Refresh and try again.");
        }
    }
}
