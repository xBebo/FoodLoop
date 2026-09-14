namespace FoodLoop.Application.Admin;
public interface IAdminService
{
    Task<DashboardSummary> GetDashboardAsync(CancellationToken ct = default);
    Task<AuditPage> GetAuditPageAsync(int page, CancellationToken ct = default);
}
