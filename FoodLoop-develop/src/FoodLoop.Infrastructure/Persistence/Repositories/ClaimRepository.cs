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
}
