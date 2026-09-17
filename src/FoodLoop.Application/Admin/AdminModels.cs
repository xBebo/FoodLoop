namespace FoodLoop.Application.Admin;
public sealed record DashboardSummary(int PendingOrganizations, int AvailableDonations, int ClosedDeliveries);
public sealed record AuditEntry(Guid Id, string Action, Guid? ActorUserId, string ActorName, string EntityType, Guid EntityId, DateTimeOffset TimestampUtc);
public sealed record AuditFilter(string? Action = null, string? Actor = null, DateTimeOffset? FromUtc = null, DateTimeOffset? ToUtc = null);
public sealed record AuditPage(IReadOnlyList<AuditEntry> Items, int Page, int PageSize, int TotalCount)
{
    public AuditFilter Filter { get; init; } = new();
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}
