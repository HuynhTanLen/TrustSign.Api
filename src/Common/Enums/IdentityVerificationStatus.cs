namespace TrustSign.Common.Enums;

public enum IdentityVerificationStatus
{
    Verified,
    /// <summary>Quản trị viên thu hồi; người dùng có thể làm eKYC lại.</summary>
    Revoked
}
