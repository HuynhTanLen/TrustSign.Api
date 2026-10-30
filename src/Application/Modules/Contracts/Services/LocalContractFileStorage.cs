using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace TrustSign.Application.Modules.Contracts;

internal sealed class LocalContractFileStorage(IOptions<ContractStorageOptions> options) : IContractFileStorage
{
    private readonly ContractStorageOptions _options = options.Value;

    public async Task SaveAsync(string storageKey, ReadOnlyMemory<byte> pdfBytes, CancellationToken cancel = default)
    {
        string fullPath = ResolveSafePath(storageKey);
        string? dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllBytesAsync(fullPath, pdfBytes, cancel);
    }

    public async Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancel = default)
    {
        string fullPath = ResolveSafePath(storageKey);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(fullPath, cancel);
    }

    public string CreateShortLivedDocumentUrl(Guid tenantId, Guid contractId, DateTimeOffset now)
    {
        long exp = now.AddMinutes(Math.Max(1, _options.DocumentUrlLifetimeMinutes)).ToUnixTimeSeconds();
        string payload = $"{tenantId:N}.{contractId:N}.{exp}";
        string sig = ComputeHmac(payload);
        string token = $"{payload}.{sig}";

        string baseUrl = _options.PublicApiBaseUrl.TrimEnd('/');
        return $"{baseUrl}/api/contracts/{contractId:D}/document?token={Uri.EscapeDataString(token)}";
    }

    public bool ValidateDocumentToken(string token, out Guid tenantId, out Guid contractId, DateTimeOffset now)
    {
        tenantId = Guid.Empty;
        contractId = Guid.Empty;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        string[] parts = token.Trim().Split('.');
        if (parts.Length != 4)
        {
            return false;
        }

        if (!Guid.TryParseExact(parts[0], "N", out tenantId) ||
            !Guid.TryParseExact(parts[1], "N", out contractId) ||
            !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out long exp))
        {
            return false;
        }

        if (now.ToUnixTimeSeconds() > exp)
        {
            return false;
        }

        string payload = $"{parts[0]}.{parts[1]}.{parts[2]}";
        string expectedSig = ComputeHmac(payload);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(parts[3]),
            Encoding.ASCII.GetBytes(expectedSig));
    }

    private string ComputeHmac(string payload)
    {
        byte[] key = Encoding.UTF8.GetBytes(_options.DocumentTokenSecret);
        byte[] data = Encoding.UTF8.GetBytes(payload);
        byte[] hash = HMACSHA256.HashData(key, data);
        return Convert.ToHexStringLower(hash);
    }

    private string ResolveSafePath(string storageKey)
    {
        string root = Path.GetFullPath(
            Path.IsPathRooted(_options.StorageRootPath)
                ? _options.StorageRootPath
                : Path.Combine(AppContext.BaseDirectory, _options.StorageRootPath));

        string sanitized = storageKey
            .Replace('\\', '/')
            .TrimStart('/');

        string fullPath = Path.GetFullPath(Path.Combine(root, sanitized));
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Đường dẫn tệp lưu trữ không hợp lệ.");
        }

        return fullPath;
    }
}
