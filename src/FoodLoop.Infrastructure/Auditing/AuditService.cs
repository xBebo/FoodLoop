using FoodLoop.Application.Interfaces.Auditing;
using FoodLoop.Application.Interfaces.Identity;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Persistence;
namespace FoodLoop.Infrastructure.Auditing;
public sealed class AuditService(ApplicationDbContext db, ICurrentUserService currentUser, TimeProvider clock) : IAuditService
{
    public void Record(string action, string entityType, Guid entityId, string? details = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        db.AuditLogs.Add(new AuditLog { ActorUserId = currentUser.UserId, Action = action,
            EntityType = entityType, EntityId = entityId, Details = details, CreatedAtUtc = clock.GetUtcNow() });
    }
}
