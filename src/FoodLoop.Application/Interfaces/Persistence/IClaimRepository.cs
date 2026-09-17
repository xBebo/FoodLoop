using FoodLoop.Domain.Entities;
namespace FoodLoop.Application.Interfaces.Persistence;
public interface IClaimRepository : IRepository<DonationClaim>
{
    Task<DonationClaim?> GetActiveForDonationAsync(Guid donationId, CancellationToken cancellationToken = default);
    // Tracked. Another organization's claim is returned as null, exactly like a missing one.
    Task<DonationClaim?> GetByIdForBeneficiaryOrganizationAsync(Guid claimId, Guid organizationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DonationClaim>> GetForBeneficiaryOrganizationAsync(Guid organizationId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
}
