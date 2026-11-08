namespace TrustSign.Common.Errors;

/// <summary>Danh mục lỗi nghiệp vụ của module Signing (F06–F08).</summary>
public static class SigningErrors
{
    public static readonly Error TokenInvalid =
        Error.NotFound("Signing.TokenInvalid", "Liên kết ký không hợp lệ hoặc đã hết hạn.");

    public static readonly Error OtpRequired =
        Error.Validation("Signing.OtpRequired", "Vui lòng xác thực mã OTP trước khi ký.");

    public static readonly Error SignatureImageInvalid =
        Error.Validation("Signing.SignatureImageInvalid", "Vui lòng vẽ chữ ký (ảnh PNG, tối đa 512 KB) trước khi ký.");
}
