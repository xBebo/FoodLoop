using FoodLoop.Application.Exceptions;
using FoodLoop.Application.Interfaces.Auditing;
using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.Donations;

public sealed class DonationExpiryService(
    IFoodDonationRepository donations,
    IUnitOfWork unitOfWork,
    IAuditService audit,
    TimeProvider clock) : IDonationExpiryService
{
    public async Task<DonationExpiryResult> ExpireDueAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var due = await donations.GetDueForExpiryAsync(now, cancellationToken);
        if (due.Count == 0) return DonationExpiryResult.Success(0);

        foreach (var donation in due)
        {
            donation.Status = DonationStatus.Expired;
            audit.Record(
                "DonationExpired",
                nameof(FoodDonation),
                donation.Id,
                $"DonationStatus={DonationStatus.Available}->{DonationStatus.Expired}");
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return DonationExpiryResult.Success(due.Count);
        }
        catch (PersistenceConflictException)
        {
            // The scoped unit of work is stale after a RowVersion race. The caller must end this run;
            // a later run with a fresh scope can evaluate the current persisted state.
            return DonationExpiryResult.Conflict();
        }
    }
}
