using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TrustSign.Common;
using TrustSign.Domain.Entities.Subscriptions;
using TrustSign.Domain.IRepositories;
using TrustSign.Domain.IRepositories.Subscriptions;

namespace TrustSign.Application.Modules.Subscriptions;

internal sealed partial class SepayWebhookService(
    IInvoiceRepository invoices,
    ISubscriptionRepository subscriptions,
    IPlanRepository plans,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<SepayWebhookService> logger) : ISepayWebhookService
{
    public async Task<Result> ProcessWebhookAsync(SepayWebhookPayload payload, CancellationToken cancel = default)
    {
        // 1. Kiểm tra tính lũy kế / idempotent (TC17): Giao dịch đã ghi nhận thì trả về thành công ngay
        if (await invoices.TransactionExistsAsync(payload.Id, cancel))
        {
            logger.LogInformation("Giao dịch SePay {TransactionId} đã được xử lý trước đó. Bỏ qua (Idempotent).", payload.Id);
            return Result.Success();
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        // 2. Tách mã hóa đơn từ nội dung thanh toán (dạng TSxxxxxxx)
        string? invoiceCode = ExtractInvoiceCode(payload.Content);

        return await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(invoiceCode))
            {
                logger.LogWarning("Giao dịch SePay {TransactionId} không tìm thấy mã hóa đơn trong nội dung: '{Content}'",
                    payload.Id, payload.Content);

                PaymentTransaction tx = PaymentTransaction.Record(
                    payload.Id,
                    payload.Gateway,
                    payload.AccountNumber,
                    payload.Content,
                    payload.ReferenceCode,
                    payload.TransferAmount,
                    payload.TransferType ?? "in",
                    payload.TransactionDate,
                    invoiceCode: null,
                    outcome: "InvoiceNotFound",
                    now);

                await invoices.AddTransactionAsync(tx, cancel);
                await invoices.SaveChangesAsync(cancel);
                return Result.Success();
            }

            Invoice? invoice = await invoices.FindByCodeAsync(invoiceCode, cancel);
            if (invoice is null)
            {
                logger.LogWarning("Không tìm thấy hóa đơn mã {InvoiceCode} cho giao dịch SePay {TransactionId}",
                    invoiceCode, payload.Id);

                PaymentTransaction tx = PaymentTransaction.Record(
                    payload.Id,
                    payload.Gateway,
                    payload.AccountNumber,
                    payload.Content,
                    payload.ReferenceCode,
                    payload.TransferAmount,
                    payload.TransferType ?? "in",
                    payload.TransactionDate,
                    invoiceCode,
                    outcome: "InvoiceNotFound",
                    now);

                await invoices.AddTransactionAsync(tx, cancel);
                await invoices.SaveChangesAsync(cancel);
                return Result.Success();
            }

            if (!string.Equals(payload.TransferType, "in", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("Giao dịch SePay {TransactionId} là chuyển tiền đi ({Type}). Bỏ qua.",
                    payload.Id, payload.TransferType);

                PaymentTransaction tx = PaymentTransaction.Record(
                    payload.Id,
                    payload.Gateway,
                    payload.AccountNumber,
                    payload.Content,
                    payload.ReferenceCode,
                    payload.TransferAmount,
                    payload.TransferType ?? "out",
                    payload.TransactionDate,
                    invoiceCode,
                    outcome: "Ignored",
                    now);

                await invoices.AddTransactionAsync(tx, cancel);
                await invoices.SaveChangesAsync(cancel);
                return Result.Success();
            }

            if (payload.TransferAmount < invoice.Amount)
            {
                logger.LogWarning(
                    "Giao dịch SePay {TransactionId} số tiền nhận ({Received}) nhỏ hơn số tiền hóa đơn {InvoiceCode} ({Expected})",
                    payload.Id, payload.TransferAmount, invoice.Code, invoice.Amount);

                PaymentTransaction tx = PaymentTransaction.Record(
                    payload.Id,
                    payload.Gateway,
                    payload.AccountNumber,
                    payload.Content,
                    payload.ReferenceCode,
                    payload.TransferAmount,
                    payload.TransferType ?? "in",
                    payload.TransactionDate,
                    invoiceCode,
                    outcome: "AmountMismatch",
                    now);

                await invoices.AddTransactionAsync(tx, cancel);
                await invoices.SaveChangesAsync(cancel);
                return Result.Success();
            }

            if (invoice.Status == Common.Enums.InvoiceStatus.Paid)
            {
                logger.LogInformation("Hóa đơn {InvoiceCode} đã được thanh toán trước đó.", invoice.Code);

                PaymentTransaction tx = PaymentTransaction.Record(
                    payload.Id,
                    payload.Gateway,
                    payload.AccountNumber,
                    payload.Content,
                    payload.ReferenceCode,
                    payload.TransferAmount,
                    payload.TransferType ?? "in",
                    payload.TransactionDate,
                    invoiceCode,
                    outcome: "AlreadyPaid",
                    now);

                await invoices.AddTransactionAsync(tx, cancel);
                await invoices.SaveChangesAsync(cancel);
                return Result.Success();
            }

            // Ghi nhận thanh toán hóa đơn
            invoice.MarkPaid(payload.Id, payload.ReferenceCode, now);

            // Cập nhật hoặc gia hạn thuê bao cho doanh nghiệp
            Plan? plan = await plans.FindByIdAsync(invoice.PlanId, cancel);
            if (plan is not null)
            {
                Subscription? subscription = await subscriptions.FindByTenantIdAsync(invoice.TenantId, cancel);
                if (subscription is null)
                {
                    subscription = Subscription.StartTrial(invoice.TenantId, plan, now);
                    subscription.ApplyPaidPlan(plan, now);
                    await subscriptions.AddAsync(subscription, cancel);
                }
                else
                {
                    subscription.ApplyPaidPlan(plan, now);
                }

                await subscriptions.SaveChangesAsync(cancel);
            }

            // Lưu giao dịch SePay với kết quả thành công (Applied)
            PaymentTransaction successTx = PaymentTransaction.Record(
                payload.Id,
                payload.Gateway,
                payload.AccountNumber,
                payload.Content,
                payload.ReferenceCode,
                payload.TransferAmount,
                payload.TransferType ?? "in",
                payload.TransactionDate,
                invoiceCode,
                outcome: "Applied",
                now);

            await invoices.AddTransactionAsync(successTx, cancel);
            await invoices.SaveChangesAsync(cancel);

            logger.LogInformation("Thanh toán thành công hóa đơn {InvoiceCode} qua SePay ({Amount} VND).",
                invoice.Code, payload.TransferAmount);

            return Result.Success();
        }, cancel);
    }

    private static string? ExtractInvoiceCode(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        Match match = InvoiceCodeRegex().Match(content);
        return match.Success ? match.Value.ToUpperInvariant() : null;
    }

    [GeneratedRegex(@"TS\d{7}", RegexOptions.IgnoreCase)]
    private static partial Regex InvoiceCodeRegex();
}
