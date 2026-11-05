using Microsoft.EntityFrameworkCore;
using TrustSign.Common.Enums;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Domain.Repositories.Identity;

internal sealed class EfIdentityVerificationRepository(TrustSignDbContext db) : IIdentityVerificationRepository
{
    public Task<IdentityVerification?> FindByIdAsync(Guid id, CancellationToken cancel) =>
        db.IdentityVerifications.FirstOrDefaultAsync(v => v.Id == id, cancel);

    public Task<IdentityVerification?> FindLatestByUserAsync(Guid userId, CancellationToken cancel) =>
        db.IdentityVerifications
            .Where(v => v.UserId == userId)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync(cancel);

    public Task<bool> IsCitizenIdVerifiedAsync(string citizenId, CancellationToken cancel) =>
        db.IdentityVerifications.AnyAsync(
            v => v.CitizenId == citizenId && v.Status == IdentityVerificationStatus.Verified,
            cancel);

    public async Task<IReadOnlyList<(IdentityVerification Verification, string Email, string AccountName)>> ListWithUsersAsync(
        int take,
        CancellationToken cancel)
    {
        var rows = await db.IdentityVerifications
            .OrderByDescending(v => v.CreatedAt)
            .Take(take)
            .Join(db.Users, v => v.UserId, u => u.Id, (v, u) => new { v, u.Email, u.FullName })
            .ToListAsync(cancel);

        return rows.Select(r => (r.v, r.Email, r.FullName)).ToList();
    }

    public async Task AddAsync(IdentityVerification verification, CancellationToken cancel)
    {
        db.IdentityVerifications.Add(verification);
        await db.SaveChangesAsync(cancel);
    }

    public Task SaveChangesAsync(CancellationToken cancel) =>
        db.SaveChangesAsync(cancel);
}
