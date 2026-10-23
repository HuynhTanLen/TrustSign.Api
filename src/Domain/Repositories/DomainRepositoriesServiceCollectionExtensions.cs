using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustSign.Domain.IRepositories.Identity;
using TrustSign.Domain.Repositories.Identity;

namespace TrustSign.Domain.Repositories;

public static class DomainRepositoriesServiceCollectionExtensions
{
    public static IServiceCollection AddDomainRepositories(this IServiceCollection services, string? connectionString)
    {
        services.AddDbContext<TrustSignDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        // Identity
        services.AddScoped<IUserRepository, EfUserRepository>();

        return services;
    }
}
