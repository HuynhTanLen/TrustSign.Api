using System.Threading.RateLimiting;
using TrustSign.Common;

namespace TrustSign.Api.Security;

public static class RateLimitingServiceCollectionExtensions
{
    public const int ReadPermitsPerMinute = 300;
    public const int WritePermitsPerMinute = 30;
    public const int AuthPermitsPerMinute = 10;

    /// <summary>
    /// Giới hạn tần suất bằng cửa sổ trượt 60 giây chia 6 đoạn (TC13, NFR04).
    /// - Endpoint đăng nhập / đăng ký: tối đa 10 lần/phút theo IP (GW-01).
    /// - Các endpoint khác: 300 đọc / 30 ghi mỗi phút theo (user hoặc IP).
    /// Phải chạy sau UseAuthentication để đọc được claim <c>sub</c>.
    /// </summary>
    public static IServiceCollection AddTrustSignRateLimiter(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (context.Request.Path.StartsWithSegments("/health"))
                {
                    return RateLimitPartition.GetNoLimiter("health");
                }

                string ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                if (HttpMethods.IsPost(context.Request.Method) && IsSensitiveAuthEndpoint(context.Request.Path))
                {
                    return RateLimitPartition.GetSlidingWindowLimiter(
                        $"{ip}:auth",
                        _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = AuthPermitsPerMinute,
                            Window = TimeSpan.FromMinutes(1),
                            SegmentsPerWindow = 6,
                            QueueLimit = 0
                        });
                }

                bool isRead = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
                string caller = context.User.FindFirst(JwtClaims.Subject)?.Value ?? ip;

                return RateLimitPartition.GetSlidingWindowLimiter(
                    $"{caller}:{(isRead ? "read" : "write")}",
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = isRead ? ReadPermitsPerMinute : WritePermitsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 6,
                        QueueLimit = 0
                    });
            });
        });

    private static bool IsSensitiveAuthEndpoint(PathString path) =>
        path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/api/auth/register", StringComparison.OrdinalIgnoreCase);
}
