namespace TrustSign.Application.Modules.Media;

public enum MediaType
{
    Pdf,
    Png,
    Jpeg,
    WebP
}

/// <summary>Nhận dạng file bằng byte đầu (magic bytes), không tin phần mở rộng hay Content-Type do client gửi.</summary>
public static class MediaContent
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static MediaType? Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 5 && bytes[..5].SequenceEqual("%PDF-"u8))
        {
            return MediaType.Pdf;
        }

        if (bytes.Length >= PngSignature.Length && bytes[..PngSignature.Length].SequenceEqual(PngSignature))
        {
            return MediaType.Png;
        }

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return MediaType.Jpeg;
        }

        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return MediaType.WebP;
        }

        return null;
    }

    public static string ContentType(MediaType type) => type switch
    {
        MediaType.Pdf => "application/pdf",
        MediaType.Png => "image/png",
        MediaType.Jpeg => "image/jpeg",
        _ => "image/webp"
    };

    /// <summary>Content-Type theo nội dung thật; không nhận ra thì trả octet-stream để trình duyệt không tự đoán và thực thi.</summary>
    public static string ContentTypeOf(ReadOnlySpan<byte> bytes) =>
        Detect(bytes) is { } type ? ContentType(type) : "application/octet-stream";
}
