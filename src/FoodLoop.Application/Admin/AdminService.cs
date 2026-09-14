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
    public Task<AuditPage> GetAuditPageAsync(int page, CancellationToken ct = default)
    {
        RequireAdmin();
        return repository.GetAuditPageAsync(Math.Max(1, page), 20, ct);
    }
    private void RequireAdmin()
    {
        if (!user.IsAuthenticated || !user.IsInRole(AppRoles.Admin))
            throw new UnauthorizedAccessException("Administrator access is required.");
    }
}
