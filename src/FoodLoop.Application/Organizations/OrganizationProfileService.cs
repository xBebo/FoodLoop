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
    public async Task<Organization?> GetMyOrganizationAsync(Guid orgId, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (userId == null) return null;

        var org = await orgRepository.GetByIdAsync(orgId, ct);
        return org;
    }

    public async Task<string?> UpdateProfileAsync(
        Guid id,
        string newName,
        string newAddress,
        byte[] rowVersion,
        CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (userId == null) return "User is not authenticated.";

        var org = await orgRepository.GetByIdAsync(id, ct);
        if (org == null) return "Organization not found or access denied.";

        if (org.Status != OrganizationStatus.Active)
            return "Only Active organizations can update their profile.";

        org.Name = newName.Trim();
        org.Address = newAddress.Trim();

        // Stage Audit Log matching your exact AuditLog Domain model
        auditRepository.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = "OrganizationUpdated",
            ActorUserId = userId,
            Details = $"Updated organization '{org.Name}' name or address.",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
            return null; // Success
        }
        catch (PersistenceConflictException)
        {
            return "The profile was modified by another operation. Please reload and try again.";
        }
    }
}