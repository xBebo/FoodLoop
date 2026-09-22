using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
namespace FoodLoop.Application.Courier;
public record CourierOption(Guid Id, string Name);
// Caller-not-permitted stays UnauthorizedAccessException (the convention shared with the admin services); these cover the rest.
public enum CourierFailureKind { Validation, NotFound, InvalidState, Conflict }
public record CourierResult(bool Succeeded, string? Error = null, string? Token = null, DateTimeOffset? ExpiresAtUtc = null, CourierFailureKind? Failure = null)
{
    public static CourierResult Fail(CourierFailureKind failure, string error) => new(false, error, Failure: failure);
}
// Only states the handover lifecycle actually produces: Booked -> PickupPending -> InTransit -> Closed.
public enum CourierNextStep { None, VerifyPickup, VerifyDelivery, Completed }
// Route fields are presentation only (names and the public pickup address), never ids.
public record CourierTaskItem(Guid ClaimId, string DonationTitle, ClaimStatus Status, CourierNextStep NextStep,
    string DonorName = "", string BeneficiaryName = "", string PickupAddress = "", DateTimeOffset ExpiresAtUtc = default);
// Admin assignment view: presentation fields only, never courier ids or organization ids.
public record AssignableClaimItem(Guid ClaimId, string DonationTitle, string DonorName, string BeneficiaryName, string PickupAddress,
    DateTimeOffset ExpiresAtUtc, DateTimeOffset ClaimedAtUtc, ClaimStatus Status, bool HasCourier);
public record HandoverTaskItem(Guid ClaimId, string DonationTitle, ClaimStatus Status, HandoverType IssueType, bool CanIssue);
public interface ICourierRepository
{
    Task<DonationClaim?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<DonationClaim>> TasksAsync(Guid courierId, CancellationToken ct);
    Task<IReadOnlyList<DonationClaim>> AssignableAsync(CancellationToken ct);
    Task<IReadOnlyList<DonationClaim>> OrganizationTasksAsync(Guid organizationId, bool donor, CancellationToken ct);
    Task<QrVerificationToken?> TokenAsync(string hash, CancellationToken ct);
    Task<IReadOnlyList<QrVerificationToken>> OutstandingAsync(Guid claimId, CancellationToken ct);
    Task<bool> HasHandoverAsync(Guid claimId, HandoverType type, CancellationToken ct);
    void AddToken(QrVerificationToken token);
    void AddHandover(HandoverRecord record);
    // Force a row-version check even when regenerating a code without changing the state.
    void Touch(DonationClaim claim);
}
public interface ICourierDirectory
{
    Task<IReadOnlyList<CourierOption>> ListAsync();
    Task<bool> IsCourierAsync(Guid id);
}
