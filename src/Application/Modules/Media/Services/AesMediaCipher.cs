using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace TrustSign.Application.Modules.Media;

internal sealed class AesMediaCipher : IMediaCipher
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public AesMediaCipher(IOptions<MediaOptions> options)
    {
        // Khóa suy ra từ bí mật lưu trữ sẵn có nên không cần thêm cấu hình. Giữ nguyên "info" cũ để đọc được ảnh eKYC đã lưu.
        _key = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            Encoding.UTF8.GetBytes(options.Value.TokenSecret),
            32,
            info: "trustsign-ekyc-images-v1"u8.ToArray());
    }

    public byte[] Encrypt(byte[] plain)
    {
        byte[] output = new byte[NonceSize + TagSize + plain.Length];
        Span<byte> nonce = output.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using AesGcm aes = new(_key, TagSize);
        aes.Encrypt(nonce, plain, output.AsSpan(NonceSize + TagSize), output.AsSpan(NonceSize, TagSize));
        return output;
    }

    public byte[] Decrypt(byte[] sealedBytes)
    {
        byte[] plain = new byte[sealedBytes.Length - NonceSize - TagSize];
        using AesGcm aes = new(_key, TagSize);
        aes.Decrypt(
            sealedBytes.AsSpan(0, NonceSize),
            sealedBytes.AsSpan(NonceSize + TagSize),
            sealedBytes.AsSpan(NonceSize, TagSize),
            plain);
        return plain;
    }
}
