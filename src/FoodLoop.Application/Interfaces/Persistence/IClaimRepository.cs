using FoodLoop.Domain.Entities;
namespace FoodLoop.Application.Interfaces.Persistence;
public interface IClaimRepository : IRepository<DonationClaim>
{
    Task<DonationClaim?> GetActiveForDonationAsync(Guid donationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DonationClaim>> GetForBeneficiaryOrganizationAsync(Guid organizationId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
}
