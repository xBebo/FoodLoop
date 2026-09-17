using FoodLoop.Domain.Entities;

namespace FoodLoop.Application.Interfaces.Persistence;

public sealed record AvailableDonationPage(IReadOnlyList<FoodDonation> Items, bool HasNext);

public interface IFoodDonationRepository : IRepository<FoodDonation>
{
    Task<AvailableDonationPage> GetAvailableAsync(
        string? search,
        Guid? categoryId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FoodDonation>> GetForDonorAsync(Guid donorOrganizationId, CancellationToken cancellationToken = default);
}
