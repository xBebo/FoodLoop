using FoodLoop.Domain.Entities;

namespace FoodLoop.Application.Interfaces.Persistence;

public interface IFoodDonationRepository : IRepository<FoodDonation>
{
    Task<IReadOnlyList<FoodDonation>> GetAvailableAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FoodDonation>> GetForDonorAsync(Guid donorOrganizationId, CancellationToken cancellationToken = default);
}
