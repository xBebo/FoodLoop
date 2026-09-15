using FoodLoop.Application.Courier;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using Microsoft.EntityFrameworkCore;
namespace FoodLoop.Infrastructure.Persistence.Repositories;
public sealed class CourierRepository(ApplicationDbContext db) : ICourierRepository
{
    private IQueryable<DonationClaim> Claims => db.DonationClaims.Include(x => x.FoodDonation).ThenInclude(x => x.DonorOrganization).Include(x => x.BeneficiaryOrganization);
    public Task<DonationClaim?> GetAsync(Guid id, CancellationToken ct) => Claims.FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<DonationClaim>> TasksAsync(Guid id, CancellationToken ct) => await Claims.AsNoTracking().Where(x => x.AssignedCourierUserId == id).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
    public async Task<IReadOnlyList<DonationClaim>> AssignableAsync(CancellationToken ct) => await Claims.AsNoTracking().Where(x => x.Status == ClaimStatus.Booked || x.Status == ClaimStatus.PickupPending).OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);
    public async Task<IReadOnlyList<DonationClaim>> OrganizationTasksAsync(Guid id, bool donor, CancellationToken ct) => await Claims.AsNoTracking().Where(x => donor ? x.FoodDonation.DonorOrganizationId == id : x.BeneficiaryOrganizationId == id).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
    public Task<QrVerificationToken?> TokenAsync(string hash, CancellationToken ct) => db.QrVerificationTokens.FirstOrDefaultAsync(x => x.TokenHash == hash, ct);
    public async Task<IReadOnlyList<QrVerificationToken>> OutstandingAsync(Guid id, CancellationToken ct) => await db.QrVerificationTokens.Where(x => x.DonationClaimId == id && x.UsedAtUtc == null).ToListAsync(ct);
    public Task<bool> HasHandoverAsync(Guid id, HandoverType type, CancellationToken ct) => db.HandoverRecords.AnyAsync(x => x.DonationClaimId == id && x.Type == type, ct);
    public void AddToken(QrVerificationToken token) => db.QrVerificationTokens.Add(token);
    public void AddHandover(HandoverRecord record) => db.HandoverRecords.Add(record);
    public void Touch(DonationClaim claim) => db.Entry(claim).Property(x => x.Status).IsModified = true;
}
