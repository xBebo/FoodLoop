using System;
using System.Threading;
using System.Threading.Tasks;
using FoodLoop.Application.Exceptions;
using FoodLoop.Application.Interfaces.Identity;
using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.Organizations;

public sealed class OrganizationProfileService(
    IRepository<Organization> orgRepository,
    IRepository<AuditLog> auditRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser)
{
    public async Task<Organization?> GetMyOrganizationAsync(CancellationToken ct = default)
    {
        var orgId = await currentUser.GetOrganizationIdAsync(ct);
        if (orgId == null) return null;

        var org = await orgRepository.GetByIdAsync(orgId.Value, ct);
        if (org == null) return null;

        if (org.Status == OrganizationStatus.Pending || org.Status == OrganizationStatus.Rejected)
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

        if (rowVersion != null && rowVersion.Length > 0)
        {
            org.RowVersion = rowVersion;
        }

        org.Name = newName.Trim();
        org.Address = newAddress.Trim();

        auditRepository.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = "OrganizationUpdated",
            ActorUserId = userId.Value,
            EntityId = org.Id,
            Details = $"Updated organization '{org.Name}' profile.",
            CreatedAtUtc = DateTimeOffset.UtcNow
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
        catch (Exception)
        {
            return "The profile was modified by another operation. Please reload and try again.";
        }
    }
}
