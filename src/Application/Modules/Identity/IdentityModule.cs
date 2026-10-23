using Microsoft.Extensions.DependencyInjection;

namespace TrustSign.Application.Modules.Identity;

/// <summary>
/// Module Identity: tài khoản (F01), doanh nghiệp thuê (F15), thành viên và vai trò (F16), cấp access token RS256.
/// <see cref="SigningKeyRing"/> do tầng Api đăng ký vì nó biết file khóa nằm ở đâu.
/// </summary>
public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IAccessTokenIssuer, RsaAccessTokenIssuer>();
        services.AddScoped<IUserService, UserService>();

        return services;
    }
}
