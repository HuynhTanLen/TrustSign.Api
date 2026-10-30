using System.ComponentModel.DataAnnotations;

namespace TrustSign.Application.Modules.Contracts;

public sealed class ContractStorageOptions
{
    public const string SectionName = "ContractStorage";

    /// <summary>Thư mục lưu file PDF hợp đồng (tương đối với ContentRoot hoặc đường dẫn tuyệt đối).</summary>
    [Required]
    public string StorageRootPath { get; set; } = "../../storage/contracts";

    [Required]
    public string PublicApiBaseUrl { get; set; } = "http://localhost:5050";

    [Required]
    public string DocumentTokenSecret { get; set; } = "trustsign_dev_document_token_hmac_secret_2026";

    public int DocumentUrlLifetimeMinutes { get; set; } = 30;
}
