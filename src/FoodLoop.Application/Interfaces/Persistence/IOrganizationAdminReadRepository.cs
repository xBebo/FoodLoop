using FoodLoop.Application.Organizations;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.Interfaces.Persistence;

public interface IOrganizationAdminReadRepository
{
    Task<OrganizationAdminPage> GetPageAsync(
        OrganizationStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<IReadOnlyList<PendingOrganizationItem>> GetPendingAsync(CancellationToken ct = default);
}
