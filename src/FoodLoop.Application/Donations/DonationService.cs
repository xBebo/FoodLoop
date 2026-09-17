using FoodLoop.Application.Identity;
using FoodLoop.Application.Exceptions;
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
    public const int MarketplacePageSize = 20;

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

        var validationError = ValidateAndNormalize(
            request.Title, request.Description, request.Quantity, request.Unit, request.PreparedAt, request.ExpiresAt,
            request.StorageInstructions, request.PickupAddress, out var fields);
        if (validationError is not null) return DonationOperationResult.Failure(validationError);

        var donation = new FoodDonation
        {
            DonorOrganizationId = organization.Id,
            FoodCategoryId = category.Id,
            Title = fields.Title,
            Description = fields.Description,
            Quantity = fields.Quantity,
            Unit = fields.Unit,
            PreparedAtUtc = fields.PreparedAtUtc,
            ExpiresAtUtc = fields.ExpiresAtUtc,
            StorageInstructions = fields.StorageInstructions,
            PickupAddress = fields.PickupAddress,
            Status = DonationStatus.Draft,
            CreatedAtUtc = clock.GetUtcNow()
        };

        donations.Add(donation);
        audit.Record("DonationCreated", nameof(FoodDonation), donation.Id);
        try { await unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (PersistenceConflictException) { return DonationOperationResult.Failure("This donation changed. Refresh and try again."); }

        return DonationOperationResult.Success(donation.Id);
    }

    public async Task<GetDonationForEditResult> GetForEditAsync(Guid donationId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.IsInRole(AppRoles.Donor))
            return new(GetDonationForEditOutcome.Forbidden);

        var organizationId = await currentUser.GetOrganizationIdAsync(cancellationToken);
        if (organizationId is null) return new(GetDonationForEditOutcome.Forbidden);

        var organization = await organizations.GetByIdAsync(organizationId.Value, cancellationToken);
        if (organization is not { Type: OrganizationType.Donor, Status: OrganizationStatus.Active })
            return new(GetDonationForEditOutcome.Forbidden);

        var donation = await donations.GetByIdAsync(donationId, cancellationToken);
        if (donation is null) return new(GetDonationForEditOutcome.NotFound);
        if (donation.DonorOrganizationId != organization.Id) return new(GetDonationForEditOutcome.Forbidden);
        if (donation.Status != DonationStatus.Draft) return new(GetDonationForEditOutcome.NotDraft);

        return new(GetDonationForEditOutcome.Success, new DonationEditItem(
            donation.Id,
            donation.FoodCategoryId,
            donation.Title,
            donation.Description,
            donation.Quantity,
            donation.Unit,
            donation.PreparedAtUtc,
            donation.ExpiresAtUtc,
            donation.StorageInstructions,
            donation.PickupAddress,
            Convert.ToBase64String(donation.RowVersion)));
    }

    public async Task<DonationOperationResult> UpdateAsync(
        Guid donationId,
        UpdateDonationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.IsInRole(AppRoles.Donor))
            return DonationOperationResult.Failure("Only donor accounts can edit donations.");

        var organizationId = await currentUser.GetOrganizationIdAsync(cancellationToken);
        if (organizationId is null)
            return DonationOperationResult.Failure("This donor account is not linked to an organization.");

        var organization = await organizations.GetByIdAsync(organizationId.Value, cancellationToken);
        if (organization is not { Type: OrganizationType.Donor, Status: OrganizationStatus.Active })
            return DonationOperationResult.Failure("Your donor organization must be active before editing donations.");

        var donation = await donations.GetByIdAsync(donationId, cancellationToken);
        if (donation is null) return DonationOperationResult.Failure("Donation was not found.");
        if (donation.DonorOrganizationId != organization.Id)
            return DonationOperationResult.Failure("You cannot edit another organization's donation.");

        byte[] submittedRowVersion;
        try { submittedRowVersion = Convert.FromBase64String(request.RowVersion ?? string.Empty); }
        catch (FormatException) { return DonationOperationResult.Failure("This donation changed. Refresh and try again."); }
        if (submittedRowVersion.Length == 0 || !donation.RowVersion.SequenceEqual(submittedRowVersion))
            return DonationOperationResult.Failure("This donation changed. Refresh and try again.");

        if (donation.Status != DonationStatus.Draft)
            return DonationOperationResult.Failure("Only draft donations can be edited.");

        var category = await categories.GetByIdAsync(request.FoodCategoryId, cancellationToken);
        if (category is null)
            return DonationOperationResult.Failure("Please choose a valid food category.");

        var validationError = ValidateAndNormalize(
            request.Title, request.Description, request.Quantity, request.Unit, request.PreparedAt, request.ExpiresAt,
            request.StorageInstructions, request.PickupAddress, out var fields);
        if (validationError is not null) return DonationOperationResult.Failure(validationError);

        donation.FoodCategoryId = category.Id;
        donation.Title = fields.Title;
        donation.Description = fields.Description;
        donation.Quantity = fields.Quantity;
        donation.Unit = fields.Unit;
        donation.PreparedAtUtc = fields.PreparedAtUtc;
        donation.ExpiresAtUtc = fields.ExpiresAtUtc;
        donation.StorageInstructions = fields.StorageInstructions;
        donation.PickupAddress = fields.PickupAddress;

        audit.Record("DonationUpdated", nameof(FoodDonation), donation.Id);
        try { await unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (PersistenceConflictException) { return DonationOperationResult.Failure("This donation changed. Refresh and try again."); }

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
        try { await unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (PersistenceConflictException) { return DonationOperationResult.Failure("This donation changed. Refresh and try again."); }

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

    public async Task<AvailableDonationsPage> GetAvailableAsync(
        string? search,
        Guid? categoryId,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        var normalizedSearch = (search ?? string.Empty).Trim();
        AvailableDonationsPage Empty() => new([], page, page > 1, false, normalizedSearch, categoryId);

        if (!currentUser.IsAuthenticated || !currentUser.IsInRole(AppRoles.Beneficiary)) return Empty();
        var organizationId = await currentUser.GetOrganizationIdAsync(cancellationToken);
        if (organizationId is null) return Empty();

        var organization = await organizations.GetByIdAsync(organizationId.Value, cancellationToken);
        if (organization is null || organization.Type != OrganizationType.Beneficiary || organization.Status != OrganizationStatus.Active)
            return Empty();

        var result = await donations.GetAvailableAsync(
            normalizedSearch, categoryId, page, MarketplacePageSize, cancellationToken);
        return new(
            result.Items.Select(x => Map(x, x.DonorOrganization?.Name)).ToList(),
            page,
            page > 1,
            result.HasNext,
            normalizedSearch,
            categoryId);
    }

    public async Task<IReadOnlyList<DonationCategoryItem>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        => (await categories.GetAllAsync(cancellationToken)).Select(x => new DonationCategoryItem(x.Id, x.Name)).ToList();

    private static DonationListItem Map(FoodDonation x, string? donorName = null)
        => new(x.Id, x.Title, x.FoodCategory?.Name ?? "Unknown", x.Quantity, x.Unit,
            x.PreparedAtUtc, x.ExpiresAtUtc, x.Status, x.PickupAddress, donorName);

    private static string? ValidateAndNormalize(
        string? title,
        string? description,
        decimal quantity,
        QuantityUnit unit,
        DateTimeOffset preparedAt,
        DateTimeOffset expiresAt,
        string? storageInstructions,
        string? pickupAddress,
        out ValidatedDonationFields fields)
    {
        fields = new(
            (title ?? string.Empty).Trim(),
            (description ?? string.Empty).Trim(),
            quantity,
            unit,
            preparedAt.ToUniversalTime(),
            expiresAt.ToUniversalTime(),
            (storageInstructions ?? string.Empty).Trim(),
            (pickupAddress ?? string.Empty).Trim());

        if (string.IsNullOrWhiteSpace(fields.Title)) return "Title is required.";
        if (fields.Title.Length > 200) return "Title cannot exceed 200 characters.";
        if (fields.Description.Length > 2000) return "Description cannot exceed 2000 characters.";
        if (fields.StorageInstructions.Length > 1000) return "Storage instructions cannot exceed 1000 characters.";
        if (string.IsNullOrWhiteSpace(fields.PickupAddress)) return "Pickup address is required.";
        if (fields.PickupAddress.Length > 500) return "Pickup address cannot exceed 500 characters.";
        if (fields.Quantity <= 0) return "Quantity must be greater than zero.";
        if (!Enum.IsDefined(fields.Unit)) return "Please choose a valid quantity unit.";
        if (fields.ExpiresAtUtc <= fields.PreparedAtUtc) return "Expiry time must be after the preparation time.";
        return null;
    }

    private sealed record ValidatedDonationFields(
        string Title,
        string Description,
        decimal Quantity,
        QuantityUnit Unit,
        DateTimeOffset PreparedAtUtc,
        DateTimeOffset ExpiresAtUtc,
        string StorageInstructions,
        string PickupAddress);
}
