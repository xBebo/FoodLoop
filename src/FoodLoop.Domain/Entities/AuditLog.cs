using FoodLoop.Domain.Common;
using FoodLoop.Domain.Enums;
namespace FoodLoop.Domain.Entities;
public sealed class AuditLog : BaseEntity
{
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string? Details { get; set; }
}
