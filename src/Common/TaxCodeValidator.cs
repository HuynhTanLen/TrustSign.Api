using System.Text.RegularExpressions;

namespace TrustSign.Common;

/// <summary>
/// Kiểm tra mã số thuế Việt Nam: 10 chữ số hoặc 13 chữ số có dấu gạch nối (^\d{10}(-\d{3})?$).
/// </summary>
public static partial class TaxCodeValidator
{
    private static readonly Regex TaxCodeRegex = new(@"^\d{10}(-\d{3})?$", RegexOptions.Compiled);

    public static bool IsValid(string? taxCode)
    {
        if (string.IsNullOrWhiteSpace(taxCode))
        {
            return false;
        }

        return TaxCodeRegex.IsMatch(taxCode.Trim());
    }
}
