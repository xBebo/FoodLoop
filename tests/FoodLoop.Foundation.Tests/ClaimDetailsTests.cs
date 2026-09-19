using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using FoodLoop.Application.Claims;
using FoodLoop.Application.Identity;
using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Foundation.Tests;
// Claim Details (Stage 3). Shares the ClaimServiceTests database, wiring and seeding helpers.
public sealed partial class ClaimServiceTests
{
    // ---- Helpers
    private sealed class CountingClaimDetails(IClaimDetailsReadRepository inner) : IClaimDetailsReadRepository
    {
        public int Lookups { get; private set; }
        public Task<ClaimDetailsEvidence?> GetForBeneficiaryOrganizationAsync(Guid claimId, Guid organizationId, CancellationToken ct = default)
        {
            Lookups++;
            return inner.GetForBeneficiaryOrganizationAsync(claimId, organizationId, ct);
        }
    }
    // Every call uses a fresh DbContext and service graph.
    private async Task<(GetClaimDetailsResult Result, int Lookups)> DetailsWithLookupsAsync(ClaimsPrincipal principal, Guid claimId)
    {
        await using var db = fixture.CreateContext();
        var spy = new CountingClaimDetails(new ClaimDetailsReadRepository(db));
        var result = await Service(db, principal, claimDetails: spy).GetDetailsAsync(claimId, CancellationToken.None);
        return (result, spy.Lookups);
    }
    private async Task<GetClaimDetailsResult> DetailsAsync(ClaimsPrincipal principal, Guid claimId) => (await DetailsWithLookupsAsync(principal, claimId)).Result;
    private async Task<ClaimDetails> OwnDetailsAsync(Guid userId, Guid claimId)
    {
        var result = await DetailsAsync(Principal(userId), claimId);
        Assert.Equal(GetClaimDetailsOutcome.Success, result.Outcome);
        return result.Details!;
    }
    private async Task<(Guid UserId, Guid ClaimId, Guid DonationId)> SeedOwnClaimAsync(OrganizationStatus beneficiaryStatus = OrganizationStatus.Active,
        ClaimStatus status = ClaimStatus.Booked, DonationStatus donationStatus = DonationStatus.Claimed, Guid? courierUserId = null)
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync(beneficiaryStatus);
        var (claimId, donationId) = await SeedClaimForCancelAsync(organizationId!.Value, status, donationStatus, courierUserId: courierUserId);
        return (userId, claimId, donationId);
    }
    private async Task<(Guid UserId, string UserName, string Email)> SeedCourierAsync(string displayName = "Courier")
    {
        await using var db = fixture.CreateContext();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(), UserName = "courier-" + Guid.NewGuid().ToString("N"), Email = Guid.NewGuid().ToString("N") + "@courier.test",
            DisplayName = displayName
        };
        db.Add(user); await db.SaveChangesAsync();
        return (user.Id, user.UserName, user.Email);
    }
    private async Task SeedEvidenceAsync(params object[] rows)
    {
        await using var db = fixture.CreateContext();
        db.AddRange(rows); await db.SaveChangesAsync();
    }
    private static AuditLog ClaimAudit(Guid entityId, string action, DateTimeOffset atUtc, string entityType = nameof(DonationClaim), string? details = null)
        => new() { Action = action, EntityType = entityType, EntityId = entityId, CreatedAtUtc = atUtc, Details = details };
    private static HandoverRecord Handover(Guid claimId, Guid courierUserId, HandoverType type, DateTimeOffset completedAtUtc)
        => new() { DonationClaimId = claimId, CourierUserId = courierUserId, Type = type, CompletedAtUtc = completedAtUtc, CreatedAtUtc = completedAtUtc };
    private static ClaimTimelineEvent Claimed(DateTimeOffset at) => new("Claimed", "Claimed", at);
    private static ClaimTimelineEvent Assigned(DateTimeOffset at) => new("CourierAssigned", "Courier assigned", at);
    private static ClaimTimelineEvent Reassigned(DateTimeOffset at) => new("CourierReassigned", "Courier reassigned", at);
    private static ClaimTimelineEvent Cancelled(DateTimeOffset at) => new("Cancelled", "Cancelled", at);
    private static ClaimTimelineEvent PickupVerified(DateTimeOffset at) => new("PickupVerified", "Pickup verified", at);
    private static ClaimTimelineEvent DeliveryVerified(DateTimeOffset at) => new("DeliveryVerified", "Delivery verified", at);
    private static ClaimTimelineEvent ClosedEvent(DateTimeOffset at) => new("Closed", "Closed", at);
    // SeedClaimForCancelAsync creates every claim at this time.
    private static DateTimeOffset ClaimedAt => Now.AddMinutes(-10);

    // ---- Access / ownership
    [Theory]
    [InlineData(OrganizationStatus.Active)]
    [InlineData(OrganizationStatus.Suspended)]
    public async Task Details_are_readable_for_own_claim(OrganizationStatus status)
    {
        var (userId, claimId, _) = await SeedOwnClaimAsync(status);
        var (result, lookups) = await DetailsWithLookupsAsync(Principal(userId), claimId);
        Assert.Equal(GetClaimDetailsOutcome.Success, result.Outcome); Assert.Equal(1, lookups);
        Assert.Equal(claimId, result.Details!.ClaimId); Assert.Equal(ClaimStatus.Booked, result.Details.Status);
        Assert.Equal(ClaimedAt, result.Details.ClaimedAtUtc);
    }
    [Fact]
    public async Task Suspended_details_have_CanCancel_false_for_an_otherwise_cancellable_claim()
    {
        var (activeUser, activeClaim, _) = await SeedOwnClaimAsync();
        var (suspendedUser, suspendedClaim, _) = await SeedOwnClaimAsync(OrganizationStatus.Suspended);
        Assert.True((await OwnDetailsAsync(activeUser, activeClaim)).CanCancel);
        Assert.False((await OwnDetailsAsync(suspendedUser, suspendedClaim)).CanCancel);
    }
    [Theory]
    [InlineData(OrganizationStatus.Active)]
    [InlineData(OrganizationStatus.Suspended)]
    public async Task Foreign_and_nonexistent_claims_are_the_same_NotFound(OrganizationStatus callerStatus)
    {
        var (userId, _) = await SeedBeneficiaryAsync(callerStatus);
        var (_, foreignClaimId, _) = await SeedOwnClaimAsync();
        var foreign = await DetailsAsync(Principal(userId), foreignClaimId);
        var missing = await DetailsAsync(Principal(userId), Guid.NewGuid());
        Assert.Equal(new GetClaimDetailsResult(GetClaimDetailsOutcome.NotFound), foreign);
        Assert.Equal(new GetClaimDetailsResult(GetClaimDetailsOutcome.NotFound), missing);
        Assert.Equal(missing, foreign);
    }
    [Theory]
    [InlineData(OrganizationStatus.Pending)]
    [InlineData(OrganizationStatus.Rejected)]
    public async Task Pending_or_rejected_beneficiary_is_denied_before_claim_lookup(OrganizationStatus status)
    {
        var (userId, ownClaimId, _) = await SeedOwnClaimAsync(status);
        Assert.Equal((new GetClaimDetailsResult(GetClaimDetailsOutcome.OrganizationNotActive), 0), await DetailsWithLookupsAsync(Principal(userId), ownClaimId));
    }
    [Fact]
    public async Task Unauthenticated_caller_is_denied_before_claim_lookup()
    {
        var (_, claimId, _) = await SeedOwnClaimAsync();
        Assert.Equal((new GetClaimDetailsResult(GetClaimDetailsOutcome.Unauthenticated), 0),
            await DetailsWithLookupsAsync(new ClaimsPrincipal(new ClaimsIdentity()), claimId));
    }
    [Theory]
    [InlineData(AppRoles.Admin)]
    [InlineData(AppRoles.Donor)]
    [InlineData(AppRoles.Courier)]
    public async Task Wrong_role_is_forbidden_before_claim_lookup(string role)
    {
        var (userId, claimId, _) = await SeedOwnClaimAsync();
        Assert.Equal((new GetClaimDetailsResult(GetClaimDetailsOutcome.Forbidden), 0), await DetailsWithLookupsAsync(Principal(userId, role), claimId));
    }
    [Fact]
    public async Task Beneficiary_without_organization_is_denied_before_claim_lookup()
    {
        var (userId, _) = await SeedUserAsync(null); var (_, claimId, _) = await SeedOwnClaimAsync();
        Assert.Equal((new GetClaimDetailsResult(GetClaimDetailsOutcome.OrganizationNotBeneficiary), 0), await DetailsWithLookupsAsync(Principal(userId), claimId));
    }
    [Fact]
    public async Task Beneficiary_linked_to_non_beneficiary_organization_is_denied_before_claim_lookup()
    {
        var (userId, organizationId) = await SeedBeneficiaryAsync(type: OrganizationType.Donor);
        var (claimId, _) = await SeedClaimForCancelAsync(organizationId!.Value); // even a claim pointing at that organization
        Assert.Equal((new GetClaimDetailsResult(GetClaimDetailsOutcome.OrganizationNotBeneficiary), 0), await DetailsWithLookupsAsync(Principal(userId), claimId));
    }

    // ---- Details data
    [Fact]
    public async Task Donation_fields_are_projected_from_the_claimed_donation()
    {
        var (userId, claimId, donationId) = await SeedOwnClaimAsync();
        var category = "Cat-" + donationId.ToString("N");
        await using (var db = fixture.CreateContext())
        {
            var donation = await db.FoodDonations.Include(x => x.FoodCategory).SingleAsync(x => x.Id == donationId);
            donation.Title = "Rice trays"; donation.Description = "Cooked rice"; donation.StorageInstructions = "Keep chilled";
            donation.PickupAddress = "1 Pickup Street"; donation.Quantity = 12.5m; donation.Unit = QuantityUnit.Kilograms;
            donation.PreparedAtUtc = Now.AddHours(-4); donation.ExpiresAtUtc = Now.AddHours(5); donation.FoodCategory.Name = category;
            await db.SaveChangesAsync();
        }
        Assert.Equal(new ClaimDonationSummary("Rice trays", category, 12.5m, QuantityUnit.Kilograms, Now.AddHours(-4), Now.AddHours(5),
            "1 Pickup Street", "Keep chilled", "Cooked rice", DonationStatus.Claimed), (await OwnDetailsAsync(userId, claimId)).Donation);
    }
    [Fact]
    public async Task No_donor_organization_data_appears_in_the_read_model()
    {
        var (userId, claimId, donationId) = await SeedOwnClaimAsync();
        Organization donor;
        await using (var db = fixture.CreateContext())
        {
            donor = (await db.FoodDonations.Include(x => x.DonorOrganization).SingleAsync(x => x.Id == donationId)).DonorOrganization;
            donor.Name = "Donor-" + Guid.NewGuid().ToString("N"); donor.Address = "Donor-address-" + Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync();
        }
        var json = JsonSerializer.Serialize(await DetailsAsync(Principal(userId), claimId));
        foreach (var value in new[] { donor.Id.ToString(), donor.Name, donor.LicenseNumber, donor.Address })
            Assert.DoesNotContain(value, json, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task Unassigned_claim_has_no_courier_display()
    {
        var (userId, claimId, _) = await SeedOwnClaimAsync();
        Assert.Null((await OwnDetailsAsync(userId, claimId)).CourierDisplayName);
    }
    [Theory]
    [InlineData("Sam Courier", "Sam Courier")]
    [InlineData("  Sam Courier  ", "Sam Courier")]
    [InlineData("", "Courier assigned")]
    [InlineData("   ", "Courier assigned")]
    public async Task Assigned_claim_shows_only_the_safe_courier_display_name(string displayName, string expected)
    {
        var courier = await SeedCourierAsync(displayName);
        var (userId, claimId, _) = await SeedOwnClaimAsync(status: ClaimStatus.PickupPending, donationStatus: DonationStatus.PickupPending, courierUserId: courier.UserId);
        var details = await OwnDetailsAsync(userId, claimId);
        Assert.Equal(expected, details.CourierDisplayName);
        Assert.False(details.CanCancel);
        var json = JsonSerializer.Serialize(details);
        foreach (var value in new[] { courier.UserId.ToString(), courier.UserName, courier.Email })
            Assert.DoesNotContain(value, json, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public void Read_model_shape_exposes_no_identity_organization_audit_or_qr_data()
    {
        string[] forbidden = ["Email", "UserName", "UserId", "CourierId", "Donor", "Organization", "License", "Actor", "AuditDetails", "Token", "Hash", "Qr"];
        Type[] types = [typeof(GetClaimDetailsResult), typeof(ClaimDetails), typeof(ClaimDonationSummary), typeof(ClaimTimelineEvent)];
        var properties = types.SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => (Type: t, Property: p))).ToList();
        foreach (var (type, property) in properties)
            Assert.False(forbidden.Any(f => property.Name.Contains(f, StringComparison.OrdinalIgnoreCase)), $"{type.Name}.{property.Name}");
        // Every property is one of these records, a timeline list, or a scalar: nothing else can ride along.
        Type[] scalars = [typeof(string), typeof(decimal), typeof(bool), typeof(DateTimeOffset), typeof(Guid)];
        foreach (var (type, property) in properties)
        {
            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            Assert.True(types.Contains(propertyType) || propertyType == typeof(IReadOnlyList<ClaimTimelineEvent>) || propertyType.IsEnum
                || scalars.Contains(propertyType), $"{type.Name}.{property.Name}: {propertyType}");
        }
        // The only identifier exposed is the claim's own id (for routing).
        Assert.Equal([$"{nameof(ClaimDetails)}.{nameof(ClaimDetails.ClaimId)}"],
            properties.Where(x => x.Property.PropertyType == typeof(Guid) || x.Property.PropertyType == typeof(Guid?)).Select(x => $"{x.Type.Name}.{x.Property.Name}"));
        Assert.Equal([nameof(ClaimTimelineEvent.Kind), nameof(ClaimTimelineEvent.Label), nameof(ClaimTimelineEvent.AtUtc)],
            typeof(ClaimTimelineEvent).GetProperties().Select(p => p.Name));
    }

    // ---- Timeline
    [Fact]
    public async Task Claimed_comes_from_the_claim_row_and_ClaimCreated_audit_is_not_duplicated()
    {
        // Real create path: writes the claim and its ClaimCreated audit at Now.
        var (userId, _) = await SeedBeneficiaryAsync(); var donationId = await SeedDonationAsync();
        var created = await CreateAsync(Principal(userId), donationId);
        Assert.Equal(CreateClaimOutcome.Created, created.Outcome);
        await SeedEvidenceAsync(ClaimAudit(created.ClaimId!.Value, "ClaimCreated", Now.AddMinutes(1))); // even a differently-timed one
        Assert.Equal([Claimed(Now)], (await OwnDetailsAsync(userId, created.ClaimId.Value)).Timeline);
    }
    [Fact]
    public async Task One_CourierAssigned_audit_is_Courier_assigned()
    {
        var (userId, claimId, _) = await SeedOwnClaimAsync();
        await SeedEvidenceAsync(ClaimAudit(claimId, "CourierAssigned", Now.AddMinutes(-8)));
        Assert.Equal([Claimed(ClaimedAt), Assigned(Now.AddMinutes(-8))], (await OwnDetailsAsync(userId, claimId)).Timeline);
    }
    [Fact]
    public async Task Later_CourierAssigned_audits_are_Courier_reassigned_in_chronological_order()
    {
        var (userId, claimId, _) = await SeedOwnClaimAsync();
        var t = Now.AddMinutes(-5); // inserted out of order, one tick apart
        await SeedEvidenceAsync(ClaimAudit(claimId, "CourierAssigned", t.AddTicks(2)), ClaimAudit(claimId, "CourierAssigned", t),
            ClaimAudit(claimId, "CourierAssigned", t.AddTicks(1)));
        Assert.Equal([Claimed(ClaimedAt), Assigned(t), Reassigned(t.AddTicks(1)), Reassigned(t.AddTicks(2))], (await OwnDetailsAsync(userId, claimId)).Timeline);
    }
    [Fact]
    public async Task Assignments_with_equal_timestamps_are_ordered_stably()
    {
        var (userId, claimId, _) = await SeedOwnClaimAsync();
        var t = Now.AddMinutes(-5);
        await SeedEvidenceAsync(ClaimAudit(claimId, "CourierAssigned", t), ClaimAudit(claimId, "CourierAssigned", t), ClaimAudit(claimId, "CourierAssigned", t));
        var first = (await OwnDetailsAsync(userId, claimId)).Timeline;
        Assert.Equal([Claimed(ClaimedAt), Assigned(t), Reassigned(t), Reassigned(t)], first);
        for (var i = 0; i < 3; i++) Assert.Equal(first, (await OwnDetailsAsync(userId, claimId)).Timeline);
    }
    [Fact]
    public async Task ClaimCancelled_audit_is_Cancelled_at_the_audit_timestamp()
    {
        var (userId, claimId, _) = await SeedOwnClaimAsync(status: ClaimStatus.Cancelled, donationStatus: DonationStatus.Available);
        await SeedEvidenceAsync(ClaimAudit(claimId, "ClaimCancelled", Now.AddMinutes(-3)));
        Assert.Equal([Claimed(ClaimedAt), Cancelled(Now.AddMinutes(-3))], (await OwnDetailsAsync(userId, claimId)).Timeline);
    }
    [Fact]
    public async Task Cancelled_status_without_evidence_has_no_fabricated_Cancelled_event()
    {
        var (userId, claimId, _) = await SeedOwnClaimAsync(status: ClaimStatus.Cancelled, donationStatus: DonationStatus.Available);
        var details = await OwnDetailsAsync(userId, claimId);
        Assert.Equal(ClaimStatus.Cancelled, details.Status);
        Assert.Equal([Claimed(ClaimedAt)], details.Timeline);
    }
    [Fact]
    public async Task Pickup_handover_is_Pickup_verified()
    {
        var courier = await SeedCourierAsync();
        var (userId, claimId, _) = await SeedOwnClaimAsync(status: ClaimStatus.InTransit, donationStatus: DonationStatus.InTransit, courierUserId: courier.UserId);
        await SeedEvidenceAsync(ClaimAudit(claimId, "CourierAssigned", Now.AddMinutes(-8)), Handover(claimId, courier.UserId, HandoverType.Pickup, Now.AddMinutes(-5)));
        Assert.Equal([Claimed(ClaimedAt), Assigned(Now.AddMinutes(-8)), PickupVerified(Now.AddMinutes(-5))], (await OwnDetailsAsync(userId, claimId)).Timeline);
    }
    [Fact]
    public async Task Delivery_handover_without_Closed_status_is_only_Delivery_verified()
    {
        var courier = await SeedCourierAsync();
        var (userId, claimId, _) = await SeedOwnClaimAsync(status: ClaimStatus.Delivered, donationStatus: DonationStatus.InTransit, courierUserId: courier.UserId);
        await SeedEvidenceAsync(Handover(claimId, courier.UserId, HandoverType.Pickup, Now.AddMinutes(-5)), Handover(claimId, courier.UserId, HandoverType.Delivery, Now.AddMinutes(-2)));
        Assert.Equal([Claimed(ClaimedAt), PickupVerified(Now.AddMinutes(-5)), DeliveryVerified(Now.AddMinutes(-2))], (await OwnDetailsAsync(userId, claimId)).Timeline);
    }
    [Fact]
    public async Task Closed_with_delivery_evidence_is_Delivery_verified_then_Closed_at_the_same_time()
    {
        var courier = await SeedCourierAsync();
        var (userId, claimId, _) = await SeedOwnClaimAsync(status: ClaimStatus.Closed, donationStatus: DonationStatus.Closed, courierUserId: courier.UserId);
        await SeedEvidenceAsync(Handover(claimId, courier.UserId, HandoverType.Delivery, Now.AddMinutes(-2)),
            Handover(claimId, courier.UserId, HandoverType.Pickup, Now.AddMinutes(-5)), ClaimAudit(claimId, "CourierAssigned", Now.AddMinutes(-8)));
        Assert.Equal([Claimed(ClaimedAt), Assigned(Now.AddMinutes(-8)), PickupVerified(Now.AddMinutes(-5)), DeliveryVerified(Now.AddMinutes(-2)), ClosedEvent(Now.AddMinutes(-2))],
            (await OwnDetailsAsync(userId, claimId)).Timeline);
    }
    [Fact]
    public async Task Closed_status_without_delivery_evidence_has_no_fabricated_Closed_event()
    {
        var courier = await SeedCourierAsync();
        var (userId, claimId, _) = await SeedOwnClaimAsync(status: ClaimStatus.Closed, donationStatus: DonationStatus.Closed, courierUserId: courier.UserId);
        await SeedEvidenceAsync(Handover(claimId, courier.UserId, HandoverType.Pickup, Now.AddMinutes(-5)));
        var details = await OwnDetailsAsync(userId, claimId);
        Assert.Equal(ClaimStatus.Closed, details.Status);
        Assert.Equal([Claimed(ClaimedAt), PickupVerified(Now.AddMinutes(-5))], details.Timeline);
    }
    [Fact]
    public async Task QR_tokens_never_contribute_to_the_timeline()
    {
        var courier = await SeedCourierAsync();
        var (userId, claimId, _) = await SeedOwnClaimAsync(status: ClaimStatus.PickupPending, donationStatus: DonationStatus.PickupPending, courierUserId: courier.UserId);
        string Hash() => Convert.ToHexString(Guid.NewGuid().ToByteArray()) + Convert.ToHexString(Guid.NewGuid().ToByteArray());
        var (used, outstanding) = (Hash(), Hash());
        await SeedEvidenceAsync(
            new QrVerificationToken { DonationClaimId = claimId, CourierUserId = courier.UserId, Purpose = QrPurpose.Pickup, TokenHash = used,
                CreatedAtUtc = Now.AddMinutes(-6), ExpiresAtUtc = Now.AddMinutes(10), UsedAtUtc = Now.AddMinutes(-4) },
            new QrVerificationToken { DonationClaimId = claimId, CourierUserId = courier.UserId, Purpose = QrPurpose.Delivery, TokenHash = outstanding,
                CreatedAtUtc = Now.AddMinutes(-3), ExpiresAtUtc = Now.AddMinutes(10) });
        var details = await OwnDetailsAsync(userId, claimId);
        Assert.Equal([Claimed(ClaimedAt)], details.Timeline);
        var json = JsonSerializer.Serialize(details);
        Assert.DoesNotContain(used, json, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain(outstanding, json, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task Non_allowlisted_audits_and_audit_details_never_appear()
    {
        var courier = await SeedCourierAsync();
        var (userId, claimId, _) = await SeedOwnClaimAsync(status: ClaimStatus.PickupPending, donationStatus: DonationStatus.PickupPending, courierUserId: courier.UserId);
        var secret = "SECRET-" + Guid.NewGuid().ToString("N");
        await SeedEvidenceAsync(
            ClaimAudit(claimId, "CourierAssigned", Now.AddMinutes(-8), details: $"CourierId={courier.UserId}; {secret}"),
            ClaimAudit(claimId, "ClaimCreated", Now.AddMinutes(-7), details: secret),
            ClaimAudit(claimId, "HandoverVerified", Now.AddMinutes(-6), details: secret),
            ClaimAudit(claimId, "courierassigned", Now.AddMinutes(-5), details: secret), // case variant is not the allowlisted action
            ClaimAudit(claimId, "SomethingElse", Now.AddMinutes(-4), details: secret));
        var details = await OwnDetailsAsync(userId, claimId);
        Assert.Equal([Claimed(ClaimedAt), Assigned(Now.AddMinutes(-8))], details.Timeline);
        var json = JsonSerializer.Serialize(details);
        Assert.DoesNotContain(secret, json); Assert.DoesNotContain(courier.UserId.ToString(), json, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task Evidence_of_another_claim_or_entity_type_is_never_mixed_in()
    {
        var courier = await SeedCourierAsync();
        var (userId, organizationId) = await SeedBeneficiaryAsync();
        var (claimId, donationId) = await SeedClaimForCancelAsync(organizationId!.Value, ClaimStatus.Closed, DonationStatus.Closed, courierUserId: courier.UserId);
        var (otherClaimId, _) = await SeedClaimForCancelAsync(organizationId.Value, ClaimStatus.Closed, DonationStatus.Closed, courierUserId: courier.UserId);
        await SeedEvidenceAsync(
            ClaimAudit(otherClaimId, "CourierAssigned", Now.AddMinutes(-8)), ClaimAudit(otherClaimId, "ClaimCancelled", Now.AddMinutes(-7)),
            Handover(otherClaimId, courier.UserId, HandoverType.Pickup, Now.AddMinutes(-5)), Handover(otherClaimId, courier.UserId, HandoverType.Delivery, Now.AddMinutes(-2)),
            ClaimAudit(claimId, "CourierAssigned", Now.AddMinutes(-6), entityType: nameof(FoodDonation)), // the claim's id under another entity type
            ClaimAudit(donationId, "ClaimCancelled", Now.AddMinutes(-4))); // the donation's id, not the claim's
        Assert.Equal([Claimed(ClaimedAt)], (await OwnDetailsAsync(userId, claimId)).Timeline);
        Assert.Equal([Claimed(ClaimedAt), Assigned(Now.AddMinutes(-8)), Cancelled(Now.AddMinutes(-7)), PickupVerified(Now.AddMinutes(-5)),
            DeliveryVerified(Now.AddMinutes(-2)), ClosedEvent(Now.AddMinutes(-2))], (await OwnDetailsAsync(userId, otherClaimId)).Timeline);
    }
    [Fact]
    public async Task Same_timestamp_events_follow_the_fixed_rank_regardless_of_insertion_order()
    {
        var courier = await SeedCourierAsync();
        var (userId, claimId, _) = await SeedOwnClaimAsync(status: ClaimStatus.Closed, donationStatus: DonationStatus.Closed, courierUserId: courier.UserId);
        await SeedEvidenceAsync(Handover(claimId, courier.UserId, HandoverType.Delivery, ClaimedAt), Handover(claimId, courier.UserId, HandoverType.Pickup, ClaimedAt),
            ClaimAudit(claimId, "ClaimCancelled", ClaimedAt), ClaimAudit(claimId, "CourierAssigned", ClaimedAt), ClaimAudit(claimId, "CourierAssigned", ClaimedAt));
        ClaimTimelineEvent[] expected = [Claimed(ClaimedAt), Assigned(ClaimedAt), Reassigned(ClaimedAt), Cancelled(ClaimedAt),
            PickupVerified(ClaimedAt), DeliveryVerified(ClaimedAt), ClosedEvent(ClaimedAt)];
        for (var i = 0; i < 3; i++) Assert.Equal(expected, (await OwnDetailsAsync(userId, claimId)).Timeline);
    }

    // ---- Cancellation regression: a real cancel, then a fresh DbContext / service graph reads the result.
    [Fact]
    public async Task Cancelled_claim_stays_readable_with_exactly_one_Cancelled_event()
    {
        var (userId, claimId, _) = await SeedOwnClaimAsync();
        Assert.True((await OwnDetailsAsync(userId, claimId)).CanCancel);
        Assert.Equal(CancelClaimOutcome.Cancelled, (await CancelAsync(Principal(userId), claimId)).Outcome);

        var details = await OwnDetailsAsync(userId, claimId);
        Assert.Equal(ClaimStatus.Cancelled, details.Status); Assert.False(details.CanCancel); Assert.Null(details.CourierDisplayName);
        Assert.Equal(DonationStatus.Available, details.Donation.Status);
        Assert.Equal([Claimed(ClaimedAt), Cancelled(Now)], details.Timeline); // AuditService stamps the fixed clock
        Assert.Equal(details.Timeline, (await OwnDetailsAsync(userId, claimId)).Timeline);

        await using var db = fixture.CreateContext();
        Assert.Equal(1, await db.AuditLogs.CountAsync(x => x.EntityId == claimId && x.Action == "ClaimCancelled"));
        Assert.Equal(1, await db.AuditLogs.CountAsync(x => x.EntityId == claimId)); // reading wrote nothing
    }
}
