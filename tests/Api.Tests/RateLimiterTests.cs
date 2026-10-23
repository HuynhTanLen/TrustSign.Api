using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TrustSign.Api.Security;
using TrustSign.Common;

namespace TrustSign.Api.Tests;

/// <summary>TC13: vượt hạn mức request trong một phút thì bị từ chối, người khác không bị ảnh hưởng.</summary>
public sealed class RateLimiterTests
{
    private readonly PartitionedRateLimiter<HttpContext> _limiter;

    public RateLimiterTests()
    {
        ServiceProvider provider = new ServiceCollection().AddTrustSignRateLimiter().BuildServiceProvider();
        _limiter = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value.GlobalLimiter!;
    }

    [Fact]
    public void Write_requests_over_the_limit_are_rejected()
    {
        HttpContext caller = Request(HttpMethods.Post, userId: "user-a");

        for (int i = 0; i < RateLimitingServiceCollectionExtensions.WritePermitsPerMinute; i++)
        {
            Assert.True(_limiter.AttemptAcquire(caller).IsAcquired, $"Request thứ {i + 1} phải được phục vụ.");
        }

        Assert.False(_limiter.AttemptAcquire(caller).IsAcquired);
    }

    [Fact]
    public void One_caller_hitting_the_limit_does_not_block_another()
    {
        HttpContext noisy = Request(HttpMethods.Post, userId: "user-a");
        for (int i = 0; i <= RateLimitingServiceCollectionExtensions.WritePermitsPerMinute; i++)
        {
            _limiter.AttemptAcquire(noisy);
        }

        Assert.True(_limiter.AttemptAcquire(Request(HttpMethods.Post, userId: "user-b")).IsAcquired);
        Assert.True(_limiter.AttemptAcquire(Request(HttpMethods.Get, userId: "user-a")).IsAcquired);
    }

    [Fact]
    public void Health_checks_are_never_limited()
    {
        HttpContext probe = Request(HttpMethods.Get, userId: null, path: "/health/live");

        for (int i = 0; i <= RateLimitingServiceCollectionExtensions.ReadPermitsPerMinute; i++)
        {
            Assert.True(_limiter.AttemptAcquire(probe).IsAcquired);
        }
    }

    private static DefaultHttpContext Request(string method, string? userId, string path = "/api/contracts")
    {
        DefaultHttpContext context = new();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        if (userId is not null)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaims.Subject, userId)], authenticationType: "Bearer"));
        }

        return context;
    }
}
