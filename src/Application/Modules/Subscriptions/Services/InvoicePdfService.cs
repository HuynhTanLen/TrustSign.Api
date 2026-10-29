using System.Globalization;
using iText.IO.Font;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using TrustSign.Common;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.Entities.Subscriptions;
using TrustSign.Domain.IRepositories.Identity;
using TrustSign.Domain.IRepositories.Subscriptions;

namespace TrustSign.Application.Modules.Subscriptions;

internal sealed class InvoicePdfService(
    IInvoiceRepository invoices,
    ITenantRepository tenants) : IInvoicePdfService
{
    private static readonly Color PrimaryBlue = new DeviceRgb(31, 95, 168); // #1f5fa8
    private static readonly Color HeaderGray = new DeviceRgb(245, 247, 250);
    private static readonly Color TextDark = new DeviceRgb(31, 35, 40);
    private static readonly Color TextMuted = new DeviceRgb(100, 110, 125);
    private static readonly Color SuccessGreen = new DeviceRgb(40, 167, 69);

    public async Task<Result<byte[]>> GenerateInvoiceReceiptPdfAsync(
        string invoiceCode,
        Guid tenantId,
        CancellationToken cancel = default)
    {
        Invoice? invoice = await invoices.FindByCodeAsync(invoiceCode.Trim().ToUpperInvariant(), cancel);
        if (invoice is null || invoice.TenantId != tenantId)
        {
            return SubscriptionErrors.InvoiceNotFound;
        }

        Tenant? tenant = await tenants.FindByIdAsync(tenantId, cancel);
        string tenantName = tenant?.Name ?? "Doanh nghiệp";
        string taxCode = string.IsNullOrWhiteSpace(tenant?.TaxCode) ? "N/A" : tenant.TaxCode;

        using var memoryStream = new MemoryStream();
        using (var writer = new PdfWriter(memoryStream))
        using (var pdf = new PdfDocument(writer))
        using (var document = new Document(pdf))
        {
            PdfFont font = ResolveFont();
            document.SetFont(font);
            document.SetMargins(36, 36, 36, 36);

            // 1. Header Banner
            var headerTable = new Table(UnitValue.CreatePercentArray(new float[] { 60, 40 })).UseAllAvailableWidth();
            headerTable.SetBorder(Border.NO_BORDER);

            var companyCell = new Cell().SetBorder(Border.NO_BORDER);
            companyCell.Add(new Paragraph("CHỮ TÍN (TRUSTSIGN)")
                .SetFontSize(18)
                .SimulateBold()
                .SetFontColor(PrimaryBlue));
            companyCell.Add(new Paragraph("Nền tảng Ký số Điện tử Doanh nghiệp chuẩn PAdES")
                .SetFontSize(10)
                .SetFontColor(TextMuted));
            headerTable.AddCell(companyCell);

            var receiptInfoCell = new Cell().SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.RIGHT);
            receiptInfoCell.Add(new Paragraph("BIÊN NHẬN THANH TOÁN")
                .SetFontSize(15)
                .SimulateBold()
                .SetFontColor(TextDark));
            receiptInfoCell.Add(new Paragraph($"Mã hóa đơn: {invoice.Code}")
                .SetFontSize(11)
                .SimulateBold()
                .SetFontColor(PrimaryBlue));
            receiptInfoCell.Add(new Paragraph($"Ngày tạo: {invoice.CreatedAt:dd/MM/yyyy HH:mm}")
                .SetFontSize(9)
                .SetFontColor(TextMuted));
            headerTable.AddCell(receiptInfoCell);

            document.Add(headerTable);
            document.Add(new Paragraph().SetMarginBottom(15));

            // 2. Customer & Status Section
            var infoTable = new Table(UnitValue.CreatePercentArray(new float[] { 50, 50 })).UseAllAvailableWidth();
            infoTable.SetBorder(new SolidBorder(new DeviceRgb(220, 225, 230), 1));
            infoTable.SetBackgroundColor(HeaderGray);

            var customerCell = new Cell().SetPadding(10).SetBorder(Border.NO_BORDER);
            customerCell.Add(new Paragraph("THÔNG TIN KHÁCH HÀNG").SetFontSize(9).SimulateBold().SetFontColor(TextMuted));
            customerCell.Add(new Paragraph(tenantName).SetFontSize(12).SimulateBold().SetFontColor(TextDark));
            customerCell.Add(new Paragraph($"Mã số thuế: {taxCode}").SetFontSize(10).SetFontColor(TextDark));
            infoTable.AddCell(customerCell);

            var paymentStatusCell = new Cell().SetPadding(10).SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.RIGHT);
            paymentStatusCell.Add(new Paragraph("TRẠNG THÁI THANH TOÁN").SetFontSize(9).SimulateBold().SetFontColor(TextMuted));

            string statusText = invoice.Status switch
            {
                Common.Enums.InvoiceStatus.Paid => "ĐÃ THANH TOÁN",
                Common.Enums.InvoiceStatus.Pending => "CHỜ THANH TOÁN",
                _ => "HẾT HẠN"
            };
            Color statusColor = invoice.Status == Common.Enums.InvoiceStatus.Paid ? SuccessGreen : PrimaryBlue;

            paymentStatusCell.Add(new Paragraph(statusText).SetFontSize(13).SimulateBold().SetFontColor(statusColor));
            if (invoice.PaidAt.HasValue)
            {
                paymentStatusCell.Add(new Paragraph($"Thanh toán lúc: {invoice.PaidAt.Value:dd/MM/yyyy HH:mm}")
                    .SetFontSize(9).SetFontColor(TextMuted));
            }
            if (invoice.SepayTransactionId.HasValue)
            {
                paymentStatusCell.Add(new Paragraph($"Mã SePay: #{invoice.SepayTransactionId.Value}")
                    .SetFontSize(9).SetFontColor(TextMuted));
            }
            infoTable.AddCell(paymentStatusCell);

            document.Add(infoTable);
            document.Add(new Paragraph().SetMarginBottom(20));

            // 3. Item Details Table
            var detailsTable = new Table(UnitValue.CreatePercentArray(new float[] { 10, 45, 20, 25 })).UseAllAvailableWidth();

            // Header Row
            detailsTable.AddHeaderCell(CreateHeaderCell("#", TextAlignment.CENTER));
            detailsTable.AddHeaderCell(CreateHeaderCell("Tên dịch vụ / Gói thuê bao", TextAlignment.LEFT));
            detailsTable.AddHeaderCell(CreateHeaderCell("Thời hạn", TextAlignment.CENTER));
            detailsTable.AddHeaderCell(CreateHeaderCell("Thành tiền (VND)", TextAlignment.RIGHT));

            // Item Row
            var itemNumCell = new Cell().SetPadding(8).SetTextAlignment(TextAlignment.CENTER).Add(new Paragraph("1"));
            var itemNameCell = new Cell().SetPadding(8).Add(new Paragraph($"Gói dịch vụ {invoice.PlanName}"));
            var itemDurationCell = new Cell().SetPadding(8).SetTextAlignment(TextAlignment.CENTER).Add(new Paragraph($"{invoice.PlanDurationDays} ngày"));
            var itemAmountCell = new Cell().SetPadding(8).SetTextAlignment(TextAlignment.RIGHT).Add(new Paragraph(invoice.Amount.ToString("N0", new CultureInfo("vi-VN"))));

            detailsTable.AddCell(itemNumCell);
            detailsTable.AddCell(itemNameCell);
            detailsTable.AddCell(itemDurationCell);
            detailsTable.AddCell(itemAmountCell);

            // Total Row
            var totalLabelCell = new Cell(1, 3)
                .SetPadding(8)
                .SetTextAlignment(TextAlignment.RIGHT)
                .Add(new Paragraph("TỔNG CỘNG:").SimulateBold());
            var totalAmountCell = new Cell()
                .SetPadding(8)
                .SetTextAlignment(TextAlignment.RIGHT)
                .SetFontSize(12)
                .SetFontColor(PrimaryBlue)
                .Add(new Paragraph($"{invoice.Amount.ToString("N0", new CultureInfo("vi-VN"))} VND").SimulateBold());

            detailsTable.AddCell(totalLabelCell);
            detailsTable.AddCell(totalAmountCell);

            document.Add(detailsTable);
            document.Add(new Paragraph().SetMarginBottom(30));

            // 4. Footer Note
            var note = new Paragraph(
                "Biên nhận này là chứng từ điện tử xác nhận giao dịch thanh toán gói dịch vụ trên nền tảng TrustSign qua cổng SePay. " +
                "Mọi thắc mắc xin vui lòng liên hệ bộ phận hỗ trợ khách hàng: support@trustsign.vn.")
                .SetFontSize(9)
                .SimulateItalic()
                .SetFontColor(TextMuted)
                .SetTextAlignment(TextAlignment.CENTER);
            document.Add(note);
        }

        return memoryStream.ToArray();
    }

    private static Cell CreateHeaderCell(string text, TextAlignment alignment)
    {
        return new Cell()
            .SetBackgroundColor(HeaderGray)
            .SetPadding(8)
            .SetTextAlignment(alignment)
            .SetFontSize(10)
            .SetFontColor(TextDark)
            .Add(new Paragraph(text).SimulateBold());
    }

    private static PdfFont ResolveFont()
    {
        try
        {
            string winFont = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
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
