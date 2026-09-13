using FoodLoop.Domain.Entities;
namespace FoodLoop.Application.Interfaces.Persistence;
public interface IClaimRepository : IRepository<DonationClaim>
{
    Task<DonationClaim?> GetActiveForDonationAsync(Guid donationId, CancellationToken cancellationToken = default);
}
