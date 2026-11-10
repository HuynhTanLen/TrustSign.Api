namespace TrustSign.Common.Validation;

/// <summary>Quy tắc mật khẩu duy nhất của hệ thống; frontend (lib/password-policy.ts) phải khớp file này.</summary>
public static class PasswordValidator
{
    public const int MinLength = 8;
    public const int MaxLength = 64; // chặn mật khẩu khổng lồ làm nặng PBKDF2

    // Chỉ liệt kê mật khẩu đã qua được luật "có chữ + có số"; loại như 12345678 đã bị luật đó chặn.
    private static readonly HashSet<string> Common = new(StringComparer.OrdinalIgnoreCase)
    {
        "password1", "password123", "qwerty123", "abc12345", "abcd1234", "a1234567",
        "admin123", "iloveyou1", "matkhau123", "1q2w3e4r", "123456abc", "welcome1",
    };

    public static bool IsAcceptable(string? password, string email, string fullName)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length is < MinLength or > MaxLength) return false;
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit)) return false;
        if (Common.Contains(password)) return false;

        string local = email.Split('@')[0];
        if (local.Length >= 3 && password.Contains(local, StringComparison.OrdinalIgnoreCase)) return false;

        string name = fullName.Replace(" ", "");
        return name.Length < 4 || !password.Replace(" ", "").Contains(name, StringComparison.OrdinalIgnoreCase);
    }
}
