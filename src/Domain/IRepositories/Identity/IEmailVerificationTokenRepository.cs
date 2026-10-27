using TrustSign.Domain.Entities.Identity;

namespace TrustSign.Domain.IRepositories.Identity;

public interface IEmailVerificationTokenRepository
{
    Task<EmailVerificationToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancel);

    Task AddAsync(EmailVerificationToken token, CancellationToken cancel);

    Task SaveChangesAsync(CancellationToken cancel);
}
