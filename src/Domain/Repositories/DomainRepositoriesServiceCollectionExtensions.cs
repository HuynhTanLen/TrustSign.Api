using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustSign.Domain.IRepositories;
using TrustSign.Domain.IRepositories.Identity;
using TrustSign.Domain.IRepositories.Subscriptions;
using TrustSign.Domain.Repositories.Identity;
using TrustSign.Domain.Repositories.Subscriptions;

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
        services.AddScoped<IEmailVerificationTokenRepository, EfEmailVerificationTokenRepository>();
        services.AddScoped<IRefreshTokenRepository, EfRefreshTokenRepository>();
        services.AddScoped<ITenantRepository, EfTenantRepository>();

        // Subscriptions
        services.AddScoped<IPlanRepository, EfPlanRepository>();
        services.AddScoped<ISubscriptionRepository, EfSubscriptionRepository>();
        services.AddScoped<IInvoiceRepository, EfInvoiceRepository>();

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        return services;
    }
}
