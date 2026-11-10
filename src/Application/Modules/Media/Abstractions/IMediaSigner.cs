namespace TrustSign.Application.Modules.Media;

/// <summary>Mục đích của token: cùng khóa HMAC nhưng token của mục đích này không dùng thay được cho mục đích khác.</summary>
public static class MediaPurposes
{
    /// <summary>Link ký gửi cho người ký. Chuỗi rỗng = định dạng cũ không có tiền tố, giữ nguyên để link đã gửi qua email (hạn 7 ngày) còn dùng được.</summary>
    public const string Signing = "";

    public const string ContractDocument = "contract-document";

    public const string Avatar = "avatar";
}

/// <summary>Phần đường dẫn sau /api/media/.</summary>
public static class MediaKinds
{
    public const string ContractDocuments = "contract-documents";

    public const string Avatars = "avatars";
}

public interface IMediaSigner
{
    /// <summary>Thời hạn mặc định của URL xem file.</summary>
    TimeSpan UrlLifetime { get; }

    /// <summary>Token dạng "{first}.{second}.{hạn}.{chữ ký}".</summary>
    string CreateToken(string purpose, Guid first, Guid second, TimeSpan lifetime, DateTimeOffset now);

    bool TryValidate(string purpose, string token, DateTimeOffset now, out Guid first, out Guid second);

    /// <summary>URL tuyệt đối /api/media/{kind}/{id}?token=...</summary>
    string CreateUrl(string kind, Guid id, string token);
}
