using System.Security.Cryptography;
using System.Text;
using FoodLoop.Application.Exceptions;
using FoodLoop.Application.Identity;
using FoodLoop.Application.Interfaces.Auditing;
using FoodLoop.Application.Interfaces.Identity;
using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
namespace FoodLoop.Application.Courier;
public sealed class CourierService(ICurrentUserService user, ICourierRepository repo, ICourierDirectory directory,
    IRepository<Organization> organizations, IUnitOfWork uow, IAuditService audit, TimeProvider clock)
{
    private void Require(string role) { if (!user.IsAuthenticated || !user.IsInRole(role) || user.UserId is null) throw new UnauthorizedAccessException(); }
    public Task<IReadOnlyList<CourierOption>> CouriersAsync() { Require(AppRoles.Admin); return directory.ListAsync(); }
    public Task<IReadOnlyList<DonationClaim>> AssignableAsync(CancellationToken ct) { Require(AppRoles.Admin); return repo.AssignableAsync(ct); }
    public Task<IReadOnlyList<DonationClaim>> MyTasksAsync(CancellationToken ct) { Require(AppRoles.Courier); return repo.TasksAsync(user.UserId!.Value, ct); }
    public async Task<DonationClaim?> MyTaskAsync(Guid id, CancellationToken ct)
    {
        Require(AppRoles.Courier);
        var claim = await repo.GetAsync(id, ct);
        if (claim != null && claim.AssignedCourierUserId != user.UserId) throw new UnauthorizedAccessException();
        return claim;
    }
    public async Task<IReadOnlyList<DonationClaim>> OrganizationTasksAsync(CancellationToken ct)
    {
        if (!user.IsAuthenticated) throw new UnauthorizedAccessException();
        var id = await user.GetOrganizationIdAsync(ct);
        var org = id is Guid key ? await organizations.GetByIdAsync(key, ct) : null;
        if (org is not { Status: OrganizationStatus.Active }) throw new UnauthorizedAccessException();
        bool donor = org.Type == OrganizationType.Donor;
        Require(donor ? AppRoles.Donor : AppRoles.Beneficiary);
        return await repo.OrganizationTasksAsync(org.Id, donor, ct);
    }
    private static bool Active(DonationClaim c) =>
        c.FoodDonation.DonorOrganization is { Type: OrganizationType.Donor, Status: OrganizationStatus.Active } &&
        c.BeneficiaryOrganization is { Type: OrganizationType.Beneficiary, Status: OrganizationStatus.Active };
    private bool Ready(DonationClaim c, HandoverType type) =>
        Active(c) && c.FoodDonation.ExpiresAtUtc > clock.GetUtcNow() &&
        (type == HandoverType.Pickup
            ? c.Status == ClaimStatus.PickupPending && c.FoodDonation.Status == DonationStatus.PickupPending
            : type == HandoverType.Delivery && c.Status == ClaimStatus.InTransit && c.FoodDonation.Status == DonationStatus.InTransit);
    private async Task RevokeAsync(Guid id, CancellationToken ct)
    {
        foreach (var token in await repo.OutstandingAsync(id, ct)) token.UsedAtUtc = clock.GetUtcNow();
    }
    private async Task<CourierResult> SaveAsync(CancellationToken ct, string? token = null)
    {
        try { await uow.SaveChangesAsync(ct); return new(true, Token: token); }
        catch (PersistenceConflictException) { return new(false, "This task changed. Refresh and try again."); }
    }
    public async Task<CourierResult> AssignAsync(Guid id, Guid courierId, CancellationToken ct)
    {
        Require(AppRoles.Admin);
        var c = await repo.GetAsync(id, ct);
        if (c == null) return new(false, "Claim not found.");
        if (!Active(c) || c.FoodDonation.ExpiresAtUtc <= clock.GetUtcNow() ||
            !((c.Status == ClaimStatus.Booked && c.FoodDonation.Status == DonationStatus.Claimed) ||
              (c.Status == ClaimStatus.PickupPending && c.FoodDonation.Status == DonationStatus.PickupPending)))
            return new(false, "Only active, unexpired claims before pickup can be assigned.");
        if (!await directory.IsCourierAsync(courierId)) return new(false, "Choose a valid courier.");
        await RevokeAsync(id, ct);
        c.AssignedCourierUserId = courierId;
        c.Status = ClaimStatus.PickupPending;
        c.FoodDonation.Status = DonationStatus.PickupPending;
        repo.Touch(c);
        audit.Record("CourierAssigned", nameof(DonationClaim), id, $"CourierId={courierId}");
        return await SaveAsync(ct);
    }
    public async Task<CourierResult> IssueAsync(Guid id, HandoverType type, CancellationToken ct)
    {
        Require(type == HandoverType.Pickup ? AppRoles.Donor : AppRoles.Beneficiary);
        var c = await repo.GetAsync(id, ct);
        if (c == null) return new(false, "Claim not found.");
        var owner = type == HandoverType.Pickup ? c.FoodDonation.DonorOrganizationId : c.BeneficiaryOrganizationId;
        if (await user.GetOrganizationIdAsync(ct) != owner) throw new UnauthorizedAccessException();
        if (!Ready(c, type) || c.AssignedCourierUserId is not Guid courierId) return new(false, "This task is not ready for that handover.");
        await RevokeAsync(id, ct);
        string raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var now = clock.GetUtcNow();
        repo.AddToken(new QrVerificationToken {
            DonationClaimId = id, CourierUserId = courierId,
            Purpose = type == HandoverType.Pickup ? QrPurpose.Pickup : QrPurpose.Delivery,
            TokenHash = Hash(raw), CreatedAtUtc = now,
            ExpiresAtUtc = c.FoodDonation.ExpiresAtUtc < now.AddMinutes(15) ? c.FoodDonation.ExpiresAtUtc : now.AddMinutes(15)
        });
        repo.Touch(c);
        audit.Record("HandoverCodeIssued", nameof(DonationClaim), id, $"Type={type}");
        return await SaveAsync(ct, raw);
    }
    private static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    public async Task<CourierResult> VerifyAsync(Guid id, string? raw, HandoverType type, CancellationToken ct)
    {
        Require(AppRoles.Courier);
        var c = await repo.GetAsync(id, ct);
        if (c == null) return new(false, "Claim not found.");
        if (c.AssignedCourierUserId != user.UserId) throw new UnauthorizedAccessException();
        if (!Enum.IsDefined(type) || !Ready(c, type)) return new(false, "The task state or organization status does not allow this handover.");
        if (string.IsNullOrWhiteSpace(raw) || raw.Length != 64) return new(false, "Invalid handover code.");
        var token = await repo.TokenAsync(Hash(raw), ct);
        var purpose = type == HandoverType.Pickup ? QrPurpose.Pickup : QrPurpose.Delivery;
        var now = clock.GetUtcNow();
        if (token == null || token.DonationClaimId != id || token.CourierUserId != user.UserId ||
            token.Purpose != purpose || token.UsedAtUtc != null || token.ExpiresAtUtc <= now)
            return new(false, "Invalid, expired or already used handover code.");
        if (await repo.HasHandoverAsync(id, type, ct)) return new(false, "This handover was already recorded.");
        if (type == HandoverType.Delivery && !await repo.HasHandoverAsync(id, HandoverType.Pickup, ct))
            return new(false, "Pickup evidence is required before delivery.");
        token.UsedAtUtc = now;
        // Verified delivery is also closure: the delivery record is the completion evidence.
        c.Status = type == HandoverType.Pickup ? ClaimStatus.InTransit : ClaimStatus.Closed;
        c.FoodDonation.Status = type == HandoverType.Pickup ? DonationStatus.InTransit : DonationStatus.Closed;
        repo.AddHandover(new HandoverRecord { DonationClaimId = id, CourierUserId = user.UserId!.Value, Type = type, CompletedAtUtc = now, CreatedAtUtc = now });
        audit.Record(type == HandoverType.Pickup ? "PickupVerified" : "DeliveryVerified", nameof(DonationClaim), id);
        return await SaveAsync(ct);
    }
}
