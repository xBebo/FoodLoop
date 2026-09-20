using FoodLoop.Application.Organizations;
using FoodLoop.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Infrastructure.Persistence.Repositories;

public sealed class OrganizationProfileRepository(ApplicationDbContext db) : IOrganizationProfileRepository
{
    public Task<Organization?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Organizations.FindAsync([id], ct).AsTask();

    public void ApplyOriginalRowVersion(Organization organization, byte[] rowVersion)
    {
        db.Entry(organization).Property(x => x.RowVersion).OriginalValue = rowVersion;
    }
}
