using TrustSign.Common.Enums;

namespace TrustSign.Application.Modules.Identity;

public interface IAccessTokenIssuer
{
    /// <summary>Cấp access token. Chỉ gọi sau khi đã kiểm tra mật khẩu và tạo phiên.</summary>
    AccessTokenDto Issue(Guid userId, Guid sessionId, UserRole role);
}
