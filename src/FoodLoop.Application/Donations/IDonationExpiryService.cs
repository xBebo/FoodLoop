namespace FoodLoop.Application.Donations;

public interface IDonationExpiryService
{
    Task<DonationExpiryResult> ExpireDueAsync(CancellationToken cancellationToken = default);
}
