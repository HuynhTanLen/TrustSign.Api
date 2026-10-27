using TrustSign.Domain.Entities.Identity;

namespace TrustSign.Domain.IRepositories.Identity;

public interface IUserRepository
{
    Task<User?> FindByIdAsync(Guid id, CancellationToken cancel);

    Task<IReadOnlyDictionary<Guid, User>> FindByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancel);

    Task<User?> FindByEmailAsync(string email, CancellationToken cancel);

    Task AddAsync(User user, CancellationToken cancel);

    Task SaveChangesAsync(CancellationToken cancel);
}
