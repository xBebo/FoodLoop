using FoodLoop.Application.Exceptions;
using FoodLoop.Application.Interfaces.Identity;
using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.Organizations;

public interface IOrganizationProfileRepository
{
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken ct = default);
    void ApplyOriginalRowVersion(Organization organization, byte[] rowVersion);
}

public sealed class OrganizationProfileService(
    IOrganizationProfileRepository orgRepository,
    IRepository<AuditLog> auditRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider clock)
{
    public async Task<Organization?> GetMyOrganizationAsync(CancellationToken ct = default)
    {
        var orgId = await currentUser.GetOrganizationIdAsync(ct);
        if (orgId == null) return null;

        var org = await orgRepository.GetByIdAsync(orgId.Value, ct);
        if (org == null) return null;

        if (org.Status is OrganizationStatus.Pending or OrganizationStatus.Rejected)
        {
            return null;
        }

        return org;
    }

    public async Task<string?> UpdateProfileAsync(
        string newName,
        string newAddress,
        byte[] rowVersion,
        CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (userId == null) return "User is not authenticated.";

        var orgId = await currentUser.GetOrganizationIdAsync(ct);
        if (orgId == null) return "Access denied: User is not associated with an organization.";

        var org = await orgRepository.GetByIdAsync(orgId.Value, ct);
        if (org == null) return "Organization not found.";

        if (org.Status != OrganizationStatus.Active)
            return "Only Active organizations are allowed to update their profile details.";

        if (rowVersion is null || rowVersion.Length == 0)
            return "The profile version is missing. Please reload and try again.";

        orgRepository.ApplyOriginalRowVersion(org, rowVersion);

        org.Name = newName.Trim();
        org.Address = newAddress.Trim();

        auditRepository.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = "OrganizationUpdated",
            ActorUserId = userId.Value,
            EntityType = nameof(Organization),
            EntityId = org.Id,
            Details = $"Updated organization '{org.Name}' profile.",
            CreatedAtUtc = clock.GetUtcNow()
        });

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
            return null;
        }
        catch (PersistenceConflictException)
        {
            return "The profile was modified by another operation. Please reload and try again.";
        }
    }
}
