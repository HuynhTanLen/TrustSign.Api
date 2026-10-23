using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TrustSign.Application.Modules.Identity;
using TrustSign.Common;

namespace TrustSign.Api.Security;

public static class JwtBearerServiceCollectionExtensions
{
    /// <summary>
    /// Nạp khóa ký và bật xác thực Bearer. Ứng dụng kiểm token bằng chính bộ khóa của mình, không cần tải JWKS.
    /// Danh tính người gọi chỉ lấy từ token đã kiểm tra, không tin header nào khác (NFR16).
    /// </summary>
    public static IServiceCollection AddTrustSignJwtAuthentication(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddSingleton(provider => SigningKeyRing.Load(
            // Đường dẫn tương đối được tính từ thư mục project Api, không phụ thuộc nơi gõ lệnh dotnet run.
            Path.Combine(environment.ContentRootPath, provider.GetRequiredService<IOptions<JwtOptions>>().Value.PrivateKeyPath),
            createIfMissing: environment.IsDevelopment()));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<SigningKeyRing, IOptions<JwtOptions>>((options, keyRing, jwt) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = CreateValidationParameters(jwt.Value.Issuer, jwt.Value.Audience);
                options.TokenValidationParameters.IssuerSigningKeys = keyRing.PublicKeys;
            });
        services.AddAuthorization();

        return services;
    }

    public static TokenValidationParameters CreateValidationParameters(string issuer, string audience) => new()
    {
        ValidIssuer = issuer,
        ValidAudience = audience,
        ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        RequireExpirationTime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = JwtClaims.Subject,
        RoleClaimType = JwtClaims.Role
    };
}
