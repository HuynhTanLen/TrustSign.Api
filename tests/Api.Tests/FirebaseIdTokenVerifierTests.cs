using Microsoft.Extensions.Options;
using TrustSign.Api.Security;

namespace TrustSign.Api.Tests;

public sealed class FirebaseIdTokenVerifierTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Rejects_everything_when_project_is_not_configured()
    {
        var verifier = new FirebaseIdTokenVerifier(Options.Create(new FirebaseOptions()));

        Assert.Null(await verifier.VerifyGoogleAsync("a.b.c", Cancel));
    }

    [Fact]
    public async Task Rejects_garbage_token_without_throwing()
    {
        var verifier = new FirebaseIdTokenVerifier(Options.Create(new FirebaseOptions { ProjectId = "trustsign-test" }));

        Assert.Null(await verifier.VerifyGoogleAsync("not-a-jwt", Cancel));
    }
}
