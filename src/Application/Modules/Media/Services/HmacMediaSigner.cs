using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace TrustSign.Application.Modules.Media;

internal sealed class HmacMediaSigner(IOptions<MediaOptions> options) : IMediaSigner
{
    private readonly MediaOptions _options = options.Value;
    private readonly byte[] _key = Encoding.UTF8.GetBytes(options.Value.TokenSecret);

    public TimeSpan UrlLifetime => TimeSpan.FromMinutes(Math.Max(1, _options.UrlLifetimeMinutes));

    public string CreateToken(string purpose, Guid first, Guid second, TimeSpan lifetime, DateTimeOffset now)
    {
        long exp = (now + lifetime).ToUnixTimeSeconds();
        string payload = $"{first:N}.{second:N}.{exp}";
        return $"{payload}.{Sign(purpose, payload)}";
    }

    public bool TryValidate(string purpose, string token, DateTimeOffset now, out Guid first, out Guid second)
    {
        first = Guid.Empty;
        second = Guid.Empty;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        string[] parts = token.Trim().Split('.');
        if (parts.Length != 4 ||
            !Guid.TryParseExact(parts[0], "N", out first) ||
            !Guid.TryParseExact(parts[1], "N", out second) ||
            !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out long exp) ||
            now.ToUnixTimeSeconds() > exp)
        {
            return false;
        }

        string expected = Sign(purpose, $"{parts[0]}.{parts[1]}.{parts[2]}");
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(parts[3]), Encoding.ASCII.GetBytes(expected));
    }

    public string CreateUrl(string kind, Guid id, string token) =>
        $"{_options.PublicApiBaseUrl.TrimEnd('/')}/api/media/{kind}/{id:D}?token={Uri.EscapeDataString(token)}";

    private string Sign(string purpose, string payload)
    {
        string data = purpose.Length == 0 ? payload : $"{purpose}:{payload}";
        return Convert.ToHexStringLower(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(data)));
    }
}
