using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Infrastructure.Persistence.Repositories;

public sealed class FoodDonationRepository(ApplicationDbContext db, TimeProvider clock)
    : Repository<FoodDonation>(db), IFoodDonationRepository
{
    public async Task<AvailableDonationPage> GetAvailableAsync(
        string? search,
        Guid? categoryId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);

        var skip = ((long)page - 1) * pageSize;
        if (skip > int.MaxValue) return new([], false);
        var now = clock.GetUtcNow();
        var normalizedSearch = search?.Trim();

        var query = Context.FoodDonations.AsNoTracking()
            .Include(x => x.FoodCategory)
            .Include(x => x.DonorOrganization)
            .Where(x => x.Status == DonationStatus.Available
                && x.ExpiresAtUtc > now
                && x.DonorOrganization.Status == OrganizationStatus.Active
                && x.DonorOrganization.Type == OrganizationType.Donor);

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
            query = query.Where(x => x.Title.Contains(normalizedSearch));
        if (categoryId is Guid selectedCategoryId)
            query = query.Where(x => x.FoodCategoryId == selectedCategoryId);

        var items = await query
            .OrderBy(x => x.ExpiresAtUtc)
            .ThenBy(x => x.Id)
            .Skip((int)skip)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasNext = items.Count > pageSize;
        if (hasNext) items.RemoveAt(items.Count - 1);
        return new(items, hasNext);
    }

    // Compatibility overload for existing concrete-repository callers. New marketplace code uses the filtered page contract above.
    public async Task<IReadOnlyList<FoodDonation>> GetAvailableAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
        => (await GetAvailableAsync(null, null, page, pageSize, cancellationToken)).Items;

    public async Task<IReadOnlyList<FoodDonation>> GetForDonorAsync(
        Guid donorOrganizationId,
        DonationStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = Context.FoodDonations.AsNoTracking()
            .Include(x => x.FoodCategory)
            .Where(x => x.DonorOrganizationId == donorOrganizationId);
        if (status is DonationStatus selectedStatus)
            query = query.Where(x => x.Status == selectedStatus);

        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<FoodDonation?> GetForDonorByIdAsync(
        Guid donationId,
        Guid donorOrganizationId,
        CancellationToken cancellationToken = default)
        => Context.FoodDonations.AsNoTracking()
            .Include(x => x.FoodCategory)
            .SingleOrDefaultAsync(x => x.Id == donationId && x.DonorOrganizationId == donorOrganizationId, cancellationToken);

    public async Task<IReadOnlyList<FoodDonation>> GetDueForExpiryAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
        => await Context.FoodDonations
            .Where(x => x.Status == DonationStatus.Available && x.ExpiresAtUtc <= nowUtc)
            .OrderBy(x => x.ExpiresAtUtc)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
}
