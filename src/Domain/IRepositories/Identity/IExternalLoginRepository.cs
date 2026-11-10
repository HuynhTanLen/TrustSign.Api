using TrustSign.Domain.Entities.Identity;

namespace TrustSign.Domain.IRepositories.Identity;

public interface IExternalLoginRepository
{
    Task<ExternalLogin?> FindAsync(string provider, string providerUserId, CancellationToken cancel);

    Task<ExternalLogin?> FindByUserAsync(Guid userId, string provider, CancellationToken cancel);

    Task AddAsync(ExternalLogin login, CancellationToken cancel);

    Task RemoveAsync(ExternalLogin login, CancellationToken cancel);
}
