using System.Globalization;

namespace TrustSign.Application.Modules.Identity;

/// <summary>
/// Dữ liệu trong mã QR của CCCD gắn chip, dạng
/// <c>Số CCCD|Số CMND cũ|Họ tên|Ngày sinh ddMMyyyy|Giới tính|Nơi thường trú|Ngày cấp ddMMyyyy</c>.
/// </summary>
public sealed record CitizenQr(
    string CitizenId,
    string FullName,
    DateOnly DateOfBirth,
    string Gender,
    string Address,
    DateOnly? IssuedOn)
{
    public static CitizenQr? TryParse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 1000)
        {
            return null;
        }

        string[] parts = raw.Trim().Split('|');
        // Mẫu thẻ mới có thể thêm trường ở cuối nên chỉ đòi tối thiểu 6 trường đầu.
        if (parts.Length < 6)
        {
            return null;
        }

        string id = parts[0].Trim();
        string name = parts[2].Trim();
        string gender = parts[4].Trim();
        string address = parts[5].Trim();

        if (id.Length != 12 || !id.All(char.IsAsciiDigit) ||
            name.Length is 0 or > 100 ||
            gender.Length is 0 or > 8 ||
            address.Length > 300 ||
            !TryParseDate(parts[3], out DateOnly dob))
        {
            return null;
        }

        DateOnly? issuedOn = parts.Length > 6 && TryParseDate(parts[6], out DateOnly issued) ? issued : null;
        return new CitizenQr(id, name, dob, gender, address, issuedOn);
    }

    private static bool TryParseDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(value.Trim(), "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    public static string Mask(string citizenId) => $"{citizenId[..3]}*******{citizenId[^2..]}";
}
