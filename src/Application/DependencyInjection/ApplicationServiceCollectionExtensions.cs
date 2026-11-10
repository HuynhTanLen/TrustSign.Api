using Microsoft.Extensions.DependencyInjection;
using TrustSign.Application.Modules.Audit;
using TrustSign.Application.Modules.Contracts;
using TrustSign.Application.Modules.Identity;
using TrustSign.Application.Modules.Media;
using TrustSign.Application.Modules.Notifications;
using TrustSign.Application.Modules.Pki;
using TrustSign.Application.Modules.Signing;
using TrustSign.Application.Modules.Subscriptions;

namespace TrustSign.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Hệ thống là một modular monolith gồm tám module; mỗi module tự đăng ký service của mình.
    /// Module chỉ gọi module khác qua interface công khai của module đó, không đụng thẳng vào entity của nhau.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        return services
            .AddIdentityModule()
            .AddSubscriptionsModule()
            .AddMediaModule()
            .AddContractsModule()
            .AddSigningModule()
            .AddPkiModule()
            .AddAuditModule()
            .AddNotificationsModule();
    }
}
