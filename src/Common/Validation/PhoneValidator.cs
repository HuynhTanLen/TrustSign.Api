using System.Text.RegularExpressions;

namespace TrustSign.Common.Validation;

/// <summary>
/// Kiểm tra định dạng số điện thoại Việt Nam: ^(0|\+84)\d{9}$
/// </summary>
public static partial class PhoneValidator
{
    private static readonly Regex PhoneRegex = new(@"^(0|\+84)\d{9}$", RegexOptions.Compiled);

    public static bool IsValid(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return false;
        }

        return PhoneRegex.IsMatch(phone.Trim());
    }
}
