using TrustSign.Application.Modules.Identity;

namespace TrustSign.Api.Tests;

public sealed class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_produces_unique_salted_hashes_and_verifies_correctly()
    {
        string hash1 = _hasher.Hash("TrustSign@2026");
        string hash2 = _hasher.Hash("TrustSign@2026");

        Assert.NotEqual(hash1, hash2);
        Assert.True(_hasher.Verify("TrustSign@2026", hash1));
        Assert.True(_hasher.Verify("TrustSign@2026", hash2));
        Assert.False(_hasher.Verify("WrongPassword!", hash1));
        Assert.False(_hasher.Verify("TrustSign@2026", "!"));
    }
}
