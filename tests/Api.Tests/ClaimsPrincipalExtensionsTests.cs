using System.Security.Claims;
using TrustSign.Api.Security;
using TrustSign.Common;

namespace TrustSign.Api.Tests;

public sealed class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetUserId_reads_the_sub_claim()
    {
        Guid userId = Guid.NewGuid();
        ClaimsPrincipal principal = PrincipalWith(new Claim(JwtClaims.Subject, userId.ToString("D")));

        Assert.Equal(userId, principal.GetUserId());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public void GetUserId_is_null_when_sub_is_not_a_guid(string subject)
    {
        ClaimsPrincipal principal = PrincipalWith(new Claim(JwtClaims.Subject, subject));

        Assert.Null(principal.GetUserId());
    }

    [Fact]
    public void GetUserId_is_null_for_an_anonymous_caller()
    {
        Assert.Null(new ClaimsPrincipal(new ClaimsIdentity()).GetUserId());
    }

    private static ClaimsPrincipal PrincipalWith(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Bearer"));
}
