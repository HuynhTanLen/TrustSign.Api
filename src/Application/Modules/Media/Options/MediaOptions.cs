using System.ComponentModel.DataAnnotations;

namespace TrustSign.Application.Modules.Media;

public sealed class MediaOptions
{
    public const string SectionName = "Media";

    /// <summary>Thư mục gốc lưu mọi file (PDF hợp đồng, ảnh chữ ký, ảnh eKYC, ảnh hồ sơ); tương đối với ContentRoot hoặc tuyệt đối.</summary>
    [Required]
    public string StorageRootPath { get; set; } = "../../storage/contracts";

    [Required]
    public string PublicApiBaseUrl { get; set; } = "http://localhost:5050";

    /// <summary>Khóa HMAC ký token/URL và là gốc suy ra khóa mã hóa ảnh eKYC; đổi giá trị là mất ảnh eKYC cũ và vô hiệu các link ký đã gửi.</summary>
    [Required]
    public string TokenSecret { get; set; } = DevTokenSecret;

    public int UrlLifetimeMinutes { get; set; } = 30;

    public const string DevTokenSecret = "trustsign_dev_document_token_hmac_secret_2026";
}
