using TrustSign.Common;

namespace TrustSign.Application.Modules.Subscriptions;

public interface IInvoicePdfService
{
    /// <summary>
    /// Xuất biên nhận thanh toán / hóa đơn nội bộ dạng PDF sử dụng iText 9 (SUB-06).
    /// </summary>
    Task<Result<byte[]>> GenerateInvoiceReceiptPdfAsync(string invoiceCode, Guid tenantId, CancellationToken cancel = default);
}
