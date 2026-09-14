using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using Microsoft.EntityFrameworkCore;
namespace FoodLoop.Infrastructure.Persistence.Repositories;
public sealed class ClaimRepository(ApplicationDbContext db) : Repository<DonationClaim>(db), IClaimRepository
{
    public Task<DonationClaim?> GetActiveForDonationAsync(Guid donationId, CancellationToken cancellationToken = default)
        => Context.DonationClaims.SingleOrDefaultAsync(x => x.FoodDonationId == donationId
            && (x.Status == ClaimStatus.Booked || x.Status == ClaimStatus.PickupPending
                || x.Status == ClaimStatus.PickedUp || x.Status == ClaimStatus.InTransit || x.Status == ClaimStatus.Delivered), cancellationToken);

    public async Task<IReadOnlyList<DonationClaim>> GetForBeneficiaryOrganizationAsync(Guid organizationId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);
        var skip = checked((page - 1) * pageSize);
        return await Context.DonationClaims.AsNoTracking().Include(x => x.FoodDonation)
            .Where(x => x.BeneficiaryOrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Skip(skip).Take(pageSize).ToListAsync(cancellationToken);
    }
}
