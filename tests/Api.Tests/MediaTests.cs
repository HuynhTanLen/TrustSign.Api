using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TrustSign.Application.Modules.Media;

namespace TrustSign.Api.Tests;

public sealed class MediaTests : IDisposable
{
    private const string Secret = "test_hmac_secret_key_for_media_tokens_2026";
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "trustsign-media-" + Guid.NewGuid().ToString("N"));
    private readonly IOptions<MediaOptions> _options;
    private readonly HmacMediaSigner _signer;

    public MediaTests()
    {
        _options = Options.Create(new MediaOptions { StorageRootPath = _dir, TokenSecret = Secret, PublicApiBaseUrl = "http://x" });
        _signer = new HmacMediaSigner(_options);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Signing_link_issued_before_the_media_module_still_validates()
    {
        // Định dạng cũ: chữ ký = HMAC(payload), không tiền tố mục đích. Link đã gửi qua email phải còn dùng được.
        Guid contractId = Guid.NewGuid(), signerId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string payload = $"{contractId:N}.{signerId:N}.{now.AddDays(1).ToUnixTimeSeconds()}";
        string legacy = payload + "." + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(payload)));

        Assert.True(_signer.TryValidate(MediaPurposes.Signing, legacy, now, out Guid c, out Guid s));
        Assert.Equal((contractId, signerId), (c, s));
    }

    [Fact]
    public void Token_of_one_purpose_is_rejected_for_another_and_when_expired_or_forged()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string doc = _signer.CreateToken(MediaPurposes.ContractDocument, a, b, TimeSpan.FromMinutes(5), now);

        Assert.True(_signer.TryValidate(MediaPurposes.ContractDocument, doc, now, out _, out _));
        Assert.False(_signer.TryValidate(MediaPurposes.Signing, doc, now, out _, out _));
        Assert.False(_signer.TryValidate(MediaPurposes.Avatar, doc, now, out _, out _));
        Assert.False(_signer.TryValidate(MediaPurposes.ContractDocument, doc, now.AddMinutes(6), out _, out _));
        Assert.False(_signer.TryValidate(MediaPurposes.ContractDocument, doc[..^1] + (doc[^1] == '0' ? '1' : '0'), now, out _, out _));
        Assert.False(_signer.TryValidate(MediaPurposes.ContractDocument, "rác", now, out _, out _));
    }

    [Fact]
    public void Detects_type_by_magic_bytes_not_by_claim()
    {
        Assert.Equal(MediaType.Pdf, MediaContent.Detect("%PDF-1.7 ..."u8));
        Assert.Equal(MediaType.Png, MediaContent.Detect([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0]));
        Assert.Equal(MediaType.Jpeg, MediaContent.Detect([0xFF, 0xD8, 0xFF, 0xE0]));
        Assert.Equal(MediaType.WebP, MediaContent.Detect("RIFF\0\0\0\0WEBPVP8 "u8));
        Assert.Null(MediaContent.Detect("<html><script>"u8));
        Assert.Equal("application/octet-stream", MediaContent.ContentTypeOf("<svg onload=x>"u8));
    }

    [Fact]
    public async Task Storage_round_trips_deletes_and_refuses_paths_outside_root()
    {
        var storage = new LocalMediaStorage(_options);
        await storage.SaveAsync("tenants/a/x.bin", new byte[] { 1, 2, 3 }, Cancel);
        Assert.Equal(new byte[] { 1, 2, 3 }, await storage.ReadAsync("tenants/a/x.bin", Cancel));

        await storage.DeleteAsync("tenants/a/x.bin", Cancel);
        Assert.Null(await storage.ReadAsync("tenants/a/x.bin", Cancel));
        await storage.DeleteAsync("tenants/a/x.bin", Cancel); // xoá file không tồn tại không lỗi

        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.ReadAsync("../outside.txt", Cancel));
    }

    [Fact]
    public void Cipher_round_trips_and_detects_tampering()
    {
        var cipher = new AesMediaCipher(_options);
        byte[] plain = [9, 8, 7, 6, 5];
        byte[] sealedBytes = cipher.Encrypt(plain);

        Assert.Equal(plain, cipher.Decrypt(sealedBytes));
        sealedBytes[^1] ^= 1;
        Assert.ThrowsAny<CryptographicException>(() => cipher.Decrypt(sealedBytes));
    }
}
