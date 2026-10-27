using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace TrustSign.Application.Modules.Identity;

/// <summary>
/// Bộ khóa ký JWT. Hiện chỉ có một khóa; khi làm xoay khóa (NFR10) thì thêm các public key cũ vào
/// <see cref="PublicKeys"/> để token chưa hết hạn vẫn kiểm tra được.
/// </summary>
public sealed class SigningKeyRing
{
    private SigningKeyRing(RSA rsa)
    {
        RsaSecurityKey publicKey = new(rsa.ExportParameters(includePrivateParameters: false));
        // kid là thumbprint của public key (RFC 7638): khóa đổi thì kid đổi, không cần cấu hình tay.
        string keyId = Base64UrlEncoder.Encode(publicKey.ComputeJwkThumbprint());
        publicKey.KeyId = keyId;

        ActiveKey = new RsaSecurityKey(rsa) { KeyId = keyId };
        PublicKeys = [publicKey];
    }

    /// <summary>Khóa có private key, chỉ dùng để ký.</summary>
    public RsaSecurityKey ActiveKey { get; }

    /// <summary>Các khóa chỉ có phần công khai: dùng để kiểm tra token và công bố qua JWKS.</summary>
    public IReadOnlyList<RsaSecurityKey> PublicKeys { get; }

    /// <summary>Nạp private key từ file PEM. Khi <paramref name="createIfMissing"/> bật (máy phát triển) thì tự sinh khóa nếu chưa có.</summary>
    public static SigningKeyRing Load(string privateKeyPath, bool createIfMissing)
    {
        string path = Path.GetFullPath(privateKeyPath);
        RSA rsa = RSA.Create(2048);

        if (File.Exists(path))
        {
            rsa.ImportFromPem(File.ReadAllText(path));
        }
        else if (createIfMissing)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, rsa.ExportPkcs8PrivateKeyPem());
        }
        else
        {
            throw new InvalidOperationException($"Không tìm thấy private key ký JWT tại '{path}'.");
        }

        return new SigningKeyRing(rsa);
    }

    public static SigningKeyRing CreateEphemeral() => new(RSA.Create(2048));
}
