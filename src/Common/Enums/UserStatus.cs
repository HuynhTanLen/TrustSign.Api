namespace TrustSign.Common.Enums;

public enum UserStatus
{
    /// <summary>Vừa đăng ký, chưa xác minh email; không đăng nhập được (BR07).</summary>
    Pending,
    Active,
    /// <summary>Bị quản trị viên khóa.</summary>
    Locked
}
