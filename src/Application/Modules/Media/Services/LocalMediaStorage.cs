using Microsoft.Extensions.Options;

namespace TrustSign.Application.Modules.Media;

internal sealed class LocalMediaStorage : IMediaStorage
{
    private readonly string _root;
    private readonly string _rootWithSeparator;

    public LocalMediaStorage(IOptions<MediaOptions> options)
    {
        string rawRoot = Path.IsPathRooted(options.Value.StorageRootPath)
            ? options.Value.StorageRootPath
            : Path.Combine(AppContext.BaseDirectory, options.Value.StorageRootPath);

        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rawRoot));
        _rootWithSeparator = _root + Path.DirectorySeparatorChar;
    }

    public async Task SaveAsync(string storageKey, ReadOnlyMemory<byte> bytes, CancellationToken cancel = default)
    {
        string fullPath = ResolveSafePath(storageKey);
        string? dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllBytesAsync(fullPath, bytes, cancel);
    }

    public async Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancel = default)
    {
        string fullPath = ResolveSafePath(storageKey);
        return File.Exists(fullPath) ? await File.ReadAllBytesAsync(fullPath, cancel) : null;
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancel = default)
    {
        File.Delete(ResolveSafePath(storageKey)); // không lỗi nếu file không tồn tại
        return Task.CompletedTask;
    }

    private string ResolveSafePath(string storageKey)
    {
        string sanitized = storageKey.Replace('\\', '/').TrimStart('/');
        string fullPath = Path.GetFullPath(Path.Combine(_root, sanitized));

        // So khớp kèm dấu phân cách thư mục để chặn prefix traversal (vd: "storage-evil" khớp tiền tố "storage").
        if (!fullPath.StartsWith(_rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Đường dẫn tệp lưu trữ không hợp lệ.");
        }

        return fullPath;
    }
}
