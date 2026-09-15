using FoodLoop.Application.Identity;
using FoodLoop.Application.Interfaces.Auditing;
using FoodLoop.Application.Interfaces.Identity;
using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.Donations;

public sealed class DonationService(
    ICurrentUserService currentUser,
    IFoodDonationRepository donations,
    IFoodCategoryRepository categories,
    IRepository<Organization> organizations,
    IUnitOfWork unitOfWork,
    IAuditService audit,
    TimeProvider clock)
{
    public async Task<DonationOperationResult> CreateAsync(CreateDonationRequest request, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.IsInRole(AppRoles.Donor))
            return DonationOperationResult.Failure("Only donor accounts can create donations.");

        var organizationId = await currentUser.GetOrganizationIdAsync(cancellationToken);
        if (organizationId is null)
            return DonationOperationResult.Failure("This donor account is not linked to an organization.");

        var organization = await organizations.GetByIdAsync(organizationId.Value, cancellationToken);
        if (organization is null || organization.Type != OrganizationType.Donor)
            return DonationOperationResult.Failure("A valid donor organization is required.");
        if (organization.Status != OrganizationStatus.Active)
            return DonationOperationResult.Failure("Your organization must be approved before creating donations.");

        var category = await categories.GetByIdAsync(request.FoodCategoryId, cancellationToken);
        if (category is null)
            return DonationOperationResult.Failure("Please choose a valid food category.");

        var title = (request.Title ?? string.Empty).Trim();
        var description = (request.Description ?? string.Empty).Trim();
        var storageInstructions = (request.StorageInstructions ?? string.Empty).Trim();
        var pickupAddress = (request.PickupAddress ?? string.Empty).Trim();
        var preparedAtUtc = request.PreparedAt.ToUniversalTime();
        var expiresAtUtc = request.ExpiresAt.ToUniversalTime();

        if (string.IsNullOrWhiteSpace(title)) return DonationOperationResult.Failure("Title is required.");
        if (title.Length > 200) return DonationOperationResult.Failure("Title cannot exceed 200 characters.");
        if (description.Length > 2000) return DonationOperationResult.Failure("Description cannot exceed 2000 characters.");
        if (storageInstructions.Length > 1000) return DonationOperationResult.Failure("Storage instructions cannot exceed 1000 characters.");
        if (string.IsNullOrWhiteSpace(pickupAddress)) return DonationOperationResult.Failure("Pickup address is required.");
        if (pickupAddress.Length > 500) return DonationOperationResult.Failure("Pickup address cannot exceed 500 characters.");
        if (request.Quantity <= 0) return DonationOperationResult.Failure("Quantity must be greater than zero.");
        if (!Enum.IsDefined(request.Unit)) return DonationOperationResult.Failure("Please choose a valid quantity unit.");
        if (expiresAtUtc <= preparedAtUtc) return DonationOperationResult.Failure("Expiry time must be after the preparation time.");

        var donation = new FoodDonation
        {
            DonorOrganizationId = organization.Id,
            FoodCategoryId = category.Id,
            Title = title,
            Description = description,
            Quantity = request.Quantity,
            Unit = request.Unit,
            PreparedAtUtc = preparedAtUtc,
            ExpiresAtUtc = expiresAtUtc,
            StorageInstructions = storageInstructions,
            PickupAddress = pickupAddress,
            Status = DonationStatus.Draft,
            CreatedAtUtc = clock.GetUtcNow()
        };

        donations.Add(donation);
        audit.Record("DonationCreated", nameof(FoodDonation), donation.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return DonationOperationResult.Success(donation.Id);
    }

    public async Task<DonationOperationResult> PublishAsync(Guid donationId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.IsInRole(AppRoles.Donor))
            return DonationOperationResult.Failure("Only donor accounts can publish donations.");

        var organizationId = await currentUser.GetOrganizationIdAsync(cancellationToken);
        if (organizationId is null)
            return DonationOperationResult.Failure("This donor account is not linked to an organization.");

        var organization = await organizations.GetByIdAsync(organizationId.Value, cancellationToken);
        if (organization is null || organization.Type != OrganizationType.Donor || organization.Status != OrganizationStatus.Active)
            return DonationOperationResult.Failure("Your donor organization must be active before publishing donations.");

        var donation = await donations.GetByIdAsync(donationId, cancellationToken);
        if (donation is null) return DonationOperationResult.Failure("Donation was not found.");
        if (donation.DonorOrganizationId != organization.Id) return DonationOperationResult.Failure("You cannot publish another organization's donation.");
        if (donation.Status != DonationStatus.Draft) return DonationOperationResult.Failure("Only draft donations can be published.");
        if (donation.Quantity <= 0) return DonationOperationResult.Failure("Quantity must be greater than zero.");
        if (donation.ExpiresAtUtc <= donation.PreparedAtUtc) return DonationOperationResult.Failure("Expiry time must be after the preparation time.");
        if (donation.ExpiresAtUtc <= clock.GetUtcNow()) return DonationOperationResult.Failure("Expired donations cannot be published.");

        donation.Status = DonationStatus.Available;
        audit.Record("DonationPublished", nameof(FoodDonation), donation.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return DonationOperationResult.Success(donation.Id);
    }

    public async Task<IReadOnlyList<DonationListItem>> GetMineAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.IsInRole(AppRoles.Donor)) return [];
        var organizationId = await currentUser.GetOrganizationIdAsync(cancellationToken);
        if (organizationId is null) return [];

        var items = await donations.GetForDonorAsync(organizationId.Value, cancellationToken);
        return items.Select(x => Map(x)).ToList();
    }

    public async Task<IReadOnlyList<DonationListItem>> GetAvailableAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.IsInRole(AppRoles.Beneficiary)) return [];
        var organizationId = await currentUser.GetOrganizationIdAsync(cancellationToken);
        if (organizationId is null) return [];

        var organization = await organizations.GetByIdAsync(organizationId.Value, cancellationToken);
        if (organization is null || organization.Type != OrganizationType.Beneficiary || organization.Status != OrganizationStatus.Active)
            return [];

        var items = await donations.GetAvailableAsync(page, pageSize, cancellationToken);
        return items.Select(x => Map(x, x.DonorOrganization?.Name)).ToList();
    }

    public async Task<IReadOnlyList<DonationCategoryItem>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        => (await categories.GetAllAsync(cancellationToken)).Select(x => new DonationCategoryItem(x.Id, x.Name)).ToList();

    private static DonationListItem Map(FoodDonation x, string? donorName = null)
        => new(x.Id, x.Title, x.FoodCategory?.Name ?? "Unknown", x.Quantity, x.Unit,
            x.PreparedAtUtc, x.ExpiresAtUtc, x.Status, x.PickupAddress, donorName);
}
