using System.ComponentModel.DataAnnotations;

namespace TrustSign.Application.Modules.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = null!;

    [Required]
    public string Audience { get; init; } = null!;

    /// <summary>Tuổi thọ access token (BR08).</summary>
    [Range(1, 60)]
    public int AccessTokenMinutes { get; init; } = 15;

    /// <summary>Đường dẫn file PEM chứa private key RSA. File này nằm ngoài mã nguồn (NFR03).</summary>
    [Required]
    public string PrivateKeyPath { get; init; } = null!;
}
