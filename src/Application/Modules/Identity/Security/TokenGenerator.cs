using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace TrustSign.Application.Modules.Identity;

public static class TokenGenerator
{
    /// <summary>Sinh token ngẫu nhiên 256-bit dạng Base64Url an toàn để đặt trong link email hoặc cookie.</summary>
    public static (string RawToken, string TokenHash) GeneratePair()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        string raw = Base64Url.EncodeToString(bytes);
        return (raw, HashToken(raw));
    }

    /// <summary>Băm SHA-256 dạng hex chữ thường (64 ký tự) để lưu database thay vì lưu token thô.</summary>
    public static string HashToken(string rawToken)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexStringLower(hash);
    }
}
