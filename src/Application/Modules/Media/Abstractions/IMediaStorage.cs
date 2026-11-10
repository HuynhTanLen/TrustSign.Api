namespace TrustSign.Application.Modules.Media;

/// <summary>Kho byte theo khóa; khóa dạng đường dẫn tương đối ("tenants/.../x.pdf") và không bao giờ thoát khỏi thư mục gốc.</summary>
public interface IMediaStorage
{
    Task SaveAsync(string storageKey, ReadOnlyMemory<byte> bytes, CancellationToken cancel = default);

    Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancel = default);

    Task DeleteAsync(string storageKey, CancellationToken cancel = default);
}
