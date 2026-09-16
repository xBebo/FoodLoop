using FoodLoop.Application.Exceptions;
using FoodLoop.Application.Identity;
using FoodLoop.Application.Interfaces.Identity;
using FoodLoop.Application.Interfaces.Auditing;
using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
namespace FoodLoop.Application.Organizations;
public sealed class OrganizationApprovalService(ICurrentUserService user, IRepository<Organization> organizations, IAuditService audit, IUnitOfWork uow)
{
    public async Task<string?> DecideAsync(Guid id, bool approve, CancellationToken ct)
    {
        if (!user.IsAuthenticated || !user.IsInRole(AppRoles.Admin)) throw new UnauthorizedAccessException();
        var org = await organizations.GetByIdAsync(id, ct);
        if (org == null) return "Organization not found.";
        if (org.Status != OrganizationStatus.Pending) return "This request has already been processed.";
        org.Status = approve ? OrganizationStatus.Active : OrganizationStatus.Rejected;
        audit.Record(approve ? "OrganizationApproved" : "OrganizationRejected", nameof(Organization), id);
        try { await uow.SaveChangesAsync(ct); return null; }
        catch (PersistenceConflictException) { return "Another administrator changed this request. Refresh and try again."; }
    }
}
