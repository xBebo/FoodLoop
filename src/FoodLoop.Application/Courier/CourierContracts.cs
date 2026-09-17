using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
namespace FoodLoop.Application.Courier;
public record CourierOption(Guid Id, string Name);
public record CourierResult(bool Succeeded, string? Error = null, string? Token = null, DateTimeOffset? ExpiresAtUtc = null);
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
