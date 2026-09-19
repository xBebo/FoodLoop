using System;
using System.Threading;
using System.Threading.Tasks;
using FoodLoop.Application.Exceptions;
using FoodLoop.Application.Interfaces.Identity;
using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.Courier;

public sealed class TaskDetailsService(
    IRepository<HandoverRecord> handoverRepository,
    IRepository<AuditLog> auditRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser)
{
    /// <summary>
    /// Fetches task details ensuring authorized courier access.
    /// </summary>
    public async Task<HandoverRecord?> GetTaskDetailsAsync(Guid taskId, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (userId == null) return null;

        var task = await handoverRepository.GetByIdAsync(taskId, ct);
        if (task == null) return null;

        return task;
    }

    /// <summary>
    /// Updates task progress with lifecycle validation matching domain contracts.
    /// </summary>
    public async Task<string?> AdvanceTaskStatusAsync(
        Guid taskId,
        HandoverType actionType,
        CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (userId == null) return "User is not authenticated.";

        var task = await handoverRepository.GetByIdAsync(taskId, ct);
        if (task == null)
            return "Task not found or access denied.";

        // Hardening: Delivery before Pickup validation
        if (actionType == HandoverType.Delivery && task.Type != HandoverType.Pickup)
        {
            return "Delivery cannot be processed before completing the pickup.";
        }

        var previousType = task.Type;
        task.Type = actionType;

        // Audit Logging for state transitions
        auditRepository.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = "CourierTaskProgressUpdated",
            ActorUserId = userId,
            Details = $"Handover Task '{task.Id}' status updated from '{previousType}' to '{actionType}'.",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
            return null; // Success
        }
        catch (PersistenceConflictException)
        {
            return "The task state was modified by another concurrent process. Please refresh and try again.";
        }
    }
}