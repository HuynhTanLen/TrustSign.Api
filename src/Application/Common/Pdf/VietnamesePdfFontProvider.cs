using iText.IO.Font;
using iText.IO.Font.Constants;
using iText.Kernel.Font;

namespace TrustSign.Application.Common.Pdf;

/// <summary>
/// Cung cấp <see cref="PdfFont"/> hỗ trợ tiếng Việt Unicode (IDENTITY_H) cho iText.
/// Dữ liệu byte của font chữ được nạp và lưu đệm một lần duy nhất trong bộ nhớ (Lazy thread-safe),
/// giúp loại bỏ hoàn toàn truy cập đĩa (File.Exists / Directory.EnumerateFiles) trên mỗi lượt sinh PDF.
/// </summary>
internal static class VietnamesePdfFontProvider
{
    private static readonly Lazy<byte[]?> CachedFontBytes = new(LoadSystemFontBytes, LazyThreadSafetyMode.ExecutionAndPublication);

    public static PdfFont CreateFont()
    {
        byte[]? fontBytes = CachedFontBytes.Value;
        if (fontBytes is not null)
        {
            FontProgram fontProgram = FontProgramFactory.CreateFont(fontBytes);
            return PdfFontFactory.CreateFont(fontProgram, PdfEncodings.IDENTITY_H);
        }

        return PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
    }

    private static byte[]? LoadSystemFontBytes()
    {
        string[] candidatePaths =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"),
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
            "/usr/share/fonts/truetype/freefont/FreeSans.ttf",
            "/usr/share/fonts/TTF/DejaVuSans.ttf",
            "/Library/Fonts/Arial.ttf"
        ];

        foreach (string fontPath in candidatePaths)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(fontPath) && File.Exists(fontPath))
                {
                    return File.ReadAllBytes(fontPath);
                }
            }
            catch
            {
                // Thử đường dẫn tiếp theo
            }
        }

        if (Directory.Exists("/usr/share/fonts"))
        {
            try
            {
                string? anyTtf = Directory
                    .EnumerateFiles("/usr/share/fonts", "*.ttf", SearchOption.AllDirectories)
                    .FirstOrDefault(f =>
                        f.Contains("DejaVuSans", StringComparison.OrdinalIgnoreCase) ||
                        f.Contains("LiberationSans", StringComparison.OrdinalIgnoreCase) ||
                        f.Contains("FreeSans", StringComparison.OrdinalIgnoreCase) ||
                        f.Contains("NotoSans", StringComparison.OrdinalIgnoreCase));

                if (anyTtf is not null)
                {
                    return File.ReadAllBytes(anyTtf);
                }
            }
            catch
            {
                // Bỏ qua nếu không có quyền đọc thư mục
            }
        }

        return null;
    }
}
