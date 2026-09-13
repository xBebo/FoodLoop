using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using Microsoft.EntityFrameworkCore;
namespace FoodLoop.Infrastructure.Persistence.Repositories;
public sealed class FoodDonationRepository(ApplicationDbContext db, TimeProvider clock)
    : Repository<FoodDonation>(db), IFoodDonationRepository
{
    public async Task<IReadOnlyList<FoodDonation>> GetAvailableAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);
        var skip = checked((page - 1) * pageSize);
        var now = clock.GetUtcNow();
        return await Context.FoodDonations.AsNoTracking()
            .Where(x => x.Status == DonationStatus.Available && x.ExpiresAtUtc > now
                && x.DonorOrganization.Status == OrganizationStatus.Active)
            .OrderBy(x => x.ExpiresAtUtc).ThenBy(x => x.Id)
            .Skip(skip).Take(pageSize).ToListAsync(cancellationToken);
    }
}
