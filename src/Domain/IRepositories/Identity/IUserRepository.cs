using TrustSign.Domain.Entities.Identity;

namespace TrustSign.Domain.IRepositories.Identity;

public interface IUserRepository
{
    Task<User?> FindByIdAsync(Guid id, CancellationToken cancel);

    Task<User?> FindByEmailAsync(string email, CancellationToken cancel);

    Task AddAsync(User user, CancellationToken cancel);
}
