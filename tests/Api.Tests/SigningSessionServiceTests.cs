using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustSign.Application.Modules.Contracts;
using TrustSign.Application.Modules.Signing;
using TrustSign.Common;
using TrustSign.Common.Errors;

namespace TrustSign.Api.Tests;

public sealed class SigningSessionServiceTests
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly LocalContractFileStorage _storage = new(Options.Create(new ContractStorageOptions
    {
        StorageRootPath = Path.GetTempPath(),
        PublicApiBaseUrl = "http://localhost:5050",
        DocumentTokenSecret = "test_hmac_secret_key_for_document_tokens_2026",
        DocumentUrlLifetimeMinutes = 30
    }));

    // VerifyOtp và nhánh chặn của SignAsync chỉ chạm token + cache, nên các phụ thuộc khác để null.
    private SigningSessionService CreateService() =>
        new(null!, null!, _storage, null!, null!, _cache, TimeProvider.System, NullLogger<SigningSessionService>.Instance);

    private string NewToken() => _storage.CreateSigningToken(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

    [Fact]
    public void VerifyOtp_locks_after_five_wrong_codes_and_accepts_correct_code_before_that()
    {
        SigningSessionService service = CreateService();
        string token = NewToken();
        _cache.Set($"signing_otp:{token}", "123456");

        for (int expected = 4; expected >= 0; expected--)
        {
            Result<OtpVerificationDto> wrong = service.VerifyOtp(token, "000000");
            Assert.Equal(new OtpVerificationDto(false, expected), wrong.Value);
        }

        Assert.Equal(new OtpVerificationDto(false, 0), service.VerifyOtp(token, "123456").Value);

        string token2 = NewToken();
        _cache.Set($"signing_otp:{token2}", "654321");
        Assert.Equal(new OtpVerificationDto(true, 5), service.VerifyOtp(token2, " 654321 ").Value);
    }

    [Fact]
    public async Task Sign_requires_verified_otp_and_rejects_bad_token()
    {
        SigningSessionService service = CreateService();

        Result noOtp = await service.SignAsync(NewToken(), TestContext.Current.CancellationToken);
        Assert.Equal(SigningErrors.OtpRequired, noOtp.Error);

        Result badToken = await service.SignAsync("not-a-token", TestContext.Current.CancellationToken);
        Assert.Equal(SigningErrors.TokenInvalid, badToken.Error);
        Assert.Equal(SigningErrors.TokenInvalid, service.VerifyOtp("not-a-token", "1").Error);
    }

    [Theory]
    [InlineData("nguyenvan@chutin.vn", "ng***@chutin.vn")]
    [InlineData("ab@x.vn", "***@x.vn")]
    [InlineData("", "")]
    public void MaskEmail_keeps_previous_format(string email, string masked) =>
        Assert.Equal(masked, SigningSessionService.MaskEmail(email));
}
