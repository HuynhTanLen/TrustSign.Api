using iText.IO.Font;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;

namespace TrustSign.Application.Modules.Contracts;

internal sealed class ContractPdfGenerator : IContractPdfGenerator
{
    private static readonly Color PrimaryBlue = new DeviceRgb(31, 95, 168);
    private static readonly Color TextDark = new DeviceRgb(31, 35, 40);
    private static readonly Color TextMuted = new DeviceRgb(100, 110, 125);

    public byte[] RenderTemplateToPdf(string title, string contractCode, string renderedContent, DateTimeOffset now)
    {
        using MemoryStream output = new();
        using (PdfWriter writer = new(output))
        using (PdfDocument pdf = new(writer))
        using (Document doc = new(pdf, PageSize.A4))
        {
            PdfFont font = ResolveFont();
            doc.SetFont(font);
            doc.SetMargins(50, 55, 50, 55);

            // Quốc hiệu & Tiêu ngữ
            doc.Add(new Paragraph("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM")
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(11)
                .SetFontColor(TextDark)
                .SimulateBold());

            doc.Add(new Paragraph("Độc lập - Tự do - Hạnh phúc")
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(10)
                .SetFontColor(TextDark)
                .SetMarginBottom(16));

            // Tiêu đề & Mã hợp đồng
            doc.Add(new Paragraph(title.Trim().ToUpperInvariant())
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(14)
                .SetFontColor(PrimaryBlue)
                .SimulateBold());

            doc.Add(new Paragraph($"Số: {contractCode} · Ngày lập: {now:dd/MM/yyyy}")
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(9.5f)
                .SetFontColor(TextMuted)
                .SetMarginBottom(18));

            // Nội dung hợp đồng từ mẫu đã điền biến
            string normalized = renderedContent.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                if (trimmedLine.Equals("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.Equals("Độc lập - Tự do - Hạnh phúc", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.Equals(title.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    doc.Add(new Paragraph(" ").SetFontSize(6));
                    continue;
                }

                Paragraph p = new Paragraph(line.TrimEnd())
                    .SetFontSize(11)
                    .SetFontColor(TextDark)
                    .SetMultipliedLeading(1.35f)
                    .SetMarginBottom(6);

                if (trimmedLine.StartsWith("ĐIỀU ", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.StartsWith("I.", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.StartsWith("II.", StringComparison.OrdinalIgnoreCase))
                {
                    p.SimulateBold();
                }

                doc.Add(p);
            }

            // Vùng đại diện các bên ký ở cuối văn bản
            doc.Add(new Paragraph(" ").SetMarginBottom(18));
            doc.Add(new Paragraph("XÁC NHẬN CHỮ KÝ ĐIỆN TỬ CỦA CÁC BÊN THAM GIA")
                .SetFontSize(9.5f)
                .SetFontColor(TextMuted)
                .SimulateBold());
        }

        return output.ToArray();
    }

    private static PdfFont ResolveFont()
    {
        try
        {
            string winFont = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
                "arial.ttf");

            if (File.Exists(winFont))
            {
                return PdfFontFactory.CreateFont(winFont, PdfEncodings.IDENTITY_H);
            }
        }
        catch
        {
            // fallback
        }

        return PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
    }
}
