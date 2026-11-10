using System.Net.Mail;

namespace TrustSign.Common.Validation;

/// <summary>
/// Kiểm tra định dạng email tập trung dùng chung cho toàn bộ Domain và Application.
/// </summary>
public static class EmailValidator
{
    public const int MaxEmailLength = 254;

    public static bool IsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        string trimmed = email.Trim();
        return trimmed.Length is > 0 and <= MaxEmailLength &&
               MailAddress.TryCreate(trimmed, out MailAddress? parsed) &&
               string.Equals(parsed.Address, trimmed, StringComparison.Ordinal);
    }
}
