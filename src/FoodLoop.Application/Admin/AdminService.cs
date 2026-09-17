using FoodLoop.Application.Identity;
using FoodLoop.Application.Interfaces.Identity;
using FoodLoop.Application.Interfaces.Persistence;
namespace FoodLoop.Application.Admin;
public sealed class AdminService(IAdminReadRepository repository, ICurrentUserService user, TimeProvider clock) : IAdminService
{
    public Task<DashboardSummary> GetDashboardAsync(CancellationToken ct = default)
    {
        RequireAdmin();
        return repository.GetDashboardAsync(clock.GetUtcNow(), ct);
    }
    public Task<AuditPage> GetAuditPageAsync(int page, AuditFilter? filter = null, CancellationToken ct = default)
    {
        RequireAdmin();
        filter ??= new AuditFilter();
        if (filter.FromUtc.HasValue && filter.ToUtc.HasValue && filter.FromUtc >= filter.ToUtc)
            throw new ArgumentException("The UTC start time must be earlier than the end time.", nameof(filter));
        return repository.GetAuditPageAsync(Math.Max(1, page), 20, filter, ct);
    }
    private void RequireAdmin()
    {
        if (!user.IsAuthenticated || !user.IsInRole(AppRoles.Admin))
            throw new UnauthorizedAccessException("Administrator access is required.");
    }
}
