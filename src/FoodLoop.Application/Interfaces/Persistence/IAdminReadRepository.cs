using FoodLoop.Application.Admin;
namespace FoodLoop.Application.Interfaces.Persistence;
public interface IAdminReadRepository
{
    Task<DashboardSummary> GetDashboardAsync(DateTimeOffset now, CancellationToken ct = default);
    Task<AuditPage> GetAuditPageAsync(int page, int pageSize, CancellationToken ct = default);
}
