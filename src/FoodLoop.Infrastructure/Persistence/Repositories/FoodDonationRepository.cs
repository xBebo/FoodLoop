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

        var skip = ((long)page - 1) * pageSize;
        if (skip > int.MaxValue) return [];
        var now = clock.GetUtcNow();

        return await Context.FoodDonations.AsNoTracking()
            .Include(x => x.FoodCategory)
            .Include(x => x.DonorOrganization)
            .Where(x => x.Status == DonationStatus.Available
                && x.ExpiresAtUtc > now
                && x.DonorOrganization.Status == OrganizationStatus.Active
                && x.DonorOrganization.Type == OrganizationType.Donor)
            .OrderBy(x => x.ExpiresAtUtc)
            .ThenBy(x => x.Id)
            .Skip((int)skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FoodDonation>> GetForDonorAsync(Guid donorOrganizationId, CancellationToken cancellationToken = default)
        => await Context.FoodDonations.AsNoTracking()
            .Include(x => x.FoodCategory)
            .Where(x => x.DonorOrganizationId == donorOrganizationId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
}
