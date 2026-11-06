using System.ComponentModel.DataAnnotations;

namespace TrustSign.Application.Modules.Notifications;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    [Required]
    public string Host { get; init; } = "localhost";

    [Range(1, 65535)]
    public int Port { get; init; } = 1025;

    public bool UseSsl { get; init; }

    public string? Username { get; init; }

    public string? Password { get; init; }

    [Required]
    public string FromEmail { get; init; } = "noreply@trustsign.local";

    [Required]
    public string FromName { get; init; } = "Chữ Tín (TrustSign)";

    [Required]
    public string FrontendBaseUrl { get; init; } = "http://localhost:3000";
}
