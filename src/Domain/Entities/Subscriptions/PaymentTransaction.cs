namespace TrustSign.Domain.Entities.Subscriptions;

/// <summary>
/// Mỗi giao dịch SePay gửi tới webhook được lưu đúng một lần (khóa duy nhất theo <see cref="SepayTransactionId"/>),
/// nhờ vậy webhook gửi lặp không gia hạn thuê bao hai lần (TC17). Giao dịch không khớp hóa đơn cũng được lưu để đối soát.
/// </summary>
public sealed class PaymentTransaction
{
    private PaymentTransaction()
    {
    }

    public Guid Id { get; private set; }

    public long SepayTransactionId { get; private set; }

    public string? Gateway { get; private set; }

    public string? AccountNumber { get; private set; }

    public string? Content { get; private set; }

    public string? ReferenceCode { get; private set; }

    public decimal TransferAmount { get; private set; }

    public string TransferType { get; private set; } = null!;

    public string? TransactionDate { get; private set; }

    /// <summary>Mã hóa đơn tách được từ nội dung chuyển khoản (có thể null nếu không tìm thấy).</summary>
    public string? InvoiceCode { get; private set; }

    /// <summary>Kết quả xử lý: <c>Applied</c>, <c>InvoiceNotFound</c>, <c>AmountMismatch</c>, <c>AlreadyPaid</c>, <c>Ignored</c>.</summary>
    public string Outcome { get; private set; } = null!;

    public DateTimeOffset ReceivedAt { get; private set; }

    public static PaymentTransaction Record(
        long sepayTransactionId,
        string? gateway,
        string? accountNumber,
        string? content,
        string? referenceCode,
        decimal transferAmount,
        string transferType,
        string? transactionDate,
        string? invoiceCode,
        string outcome,
        DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        SepayTransactionId = sepayTransactionId,
        Gateway = Truncate(gateway, 100),
        AccountNumber = Truncate(accountNumber, 50),
        Content = Truncate(content, 500),
        ReferenceCode = Truncate(referenceCode, 100),
        TransferAmount = transferAmount,
        TransferType = Truncate(transferType, 10) ?? "in",
        TransactionDate = Truncate(transactionDate, 40),
        InvoiceCode = invoiceCode,
        Outcome = outcome,
        ReceivedAt = now
    };

    private static string? Truncate(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];
}
