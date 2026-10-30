namespace TrustSign.Application.Modules.Contracts;

public interface IContractFileStorage
{
    Task SaveAsync(string storageKey, ReadOnlyMemory<byte> pdfBytes, CancellationToken cancel = default);

    Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancel = default);

    /// <summary>
    /// Tạo đường dẫn URL có chữ ký HMAC ngắn hạn để trình duyệt hiển thị file PDF trong iframe (F02, CON-02).
    /// </summary>
    string CreateShortLivedDocumentUrl(Guid tenantId, Guid contractId, DateTimeOffset now);

    /// <summary>
    /// Kiểm tra chữ ký HMAC và thời hạn của token xem tài liệu.
    /// </summary>
    bool ValidateDocumentToken(string token, out Guid tenantId, out Guid contractId, DateTimeOffset now);
}
