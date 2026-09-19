using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;

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
    Task<IReadOnlyList<FoodDonation>> GetForDonorAsync(
        Guid donorOrganizationId,
        DonationStatus? status = null,
        CancellationToken cancellationToken = default);
    Task<FoodDonation?> GetForDonorByIdAsync(
        Guid donationId,
        Guid donorOrganizationId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FoodDonation>> GetDueForExpiryAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}
