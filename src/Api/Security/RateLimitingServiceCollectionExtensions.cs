using System.Threading.RateLimiting;
using TrustSign.Common;

namespace TrustSign.Api.Security;

public static class RateLimitingServiceCollectionExtensions
{
    public const int ReadPermitsPerMinute = 300;
    public const int WritePermitsPerMinute = 30;

    /// <summary>
    /// Giới hạn tần suất bằng cửa sổ trượt 60 giây chia 6 đoạn (TC13). Ngăn đếm = (user hoặc IP) x (đọc hoặc ghi).
    /// Bộ đếm nằm trong bộ nhớ của tiến trình. Phải chạy sau UseAuthentication để đọc được claim <c>sub</c>.
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

                bool isRead = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
                string caller = context.User.FindFirst(JwtClaims.Subject)?.Value
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";

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
}
