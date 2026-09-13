namespace FoodLoop.Application.Interfaces.Auditing;
public interface IAuditService
{
    // Stages a record in the current unit of work; does not save independently.
    // Details must never contain passwords, raw tokens, or unnecessary personal data.
    void Record(string action, string entityType, Guid entityId, string? details = null);
}
