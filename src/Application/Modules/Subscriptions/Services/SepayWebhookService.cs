using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TrustSign.Common;
using TrustSign.Common.Enums;
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
        DateTimeOffset now = timeProvider.GetUtcNow();
        string? invoiceCode = ExtractInvoiceCode(payload.Content);

        return await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // 1. Kiểm tra tính lũy kế / idempotent bên trong transaction để chống race condition (TC17)
            if (await invoices.TransactionExistsAsync(payload.Id, cancel))
            {
                logger.LogInformation("Giao dịch SePay {TransactionId} đã được xử lý trước đó. Bỏ qua (Idempotent).", payload.Id);
                return Result.Success();
            }

            if (string.IsNullOrWhiteSpace(invoiceCode))
            {
                logger.LogWarning("Giao dịch SePay {TransactionId} không tìm thấy mã hóa đơn trong nội dung: '{Content}'",
                    payload.Id, payload.Content);

                return await RecordOutcomeAsync(payload, invoiceCode: null, outcome: "InvoiceNotFound", defaultTransferType: "in", now, cancel);
            }

            Invoice? invoice = await invoices.FindByCodeAsync(invoiceCode, cancel);
            if (invoice is null)
            {
                logger.LogWarning("Không tìm thấy hóa đơn mã {InvoiceCode} cho giao dịch SePay {TransactionId}",
                    invoiceCode, payload.Id);

                return await RecordOutcomeAsync(payload, invoiceCode, outcome: "InvoiceNotFound", defaultTransferType: "in", now, cancel);
            }

            if (!string.Equals(payload.TransferType, "in", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("Giao dịch SePay {TransactionId} là chuyển tiền đi ({Type}). Bỏ qua.",
                    payload.Id, payload.TransferType);

                return await RecordOutcomeAsync(payload, invoiceCode, outcome: "Ignored", defaultTransferType: "out", now, cancel);
            }

            if (payload.TransferAmount < invoice.Amount)
            {
                logger.LogWarning(
                    "Giao dịch SePay {TransactionId} số tiền nhận ({Received}) nhỏ hơn số tiền hóa đơn {InvoiceCode} ({Expected})",
                    payload.Id, payload.TransferAmount, invoice.Code, invoice.Amount);

                return await RecordOutcomeAsync(payload, invoiceCode, outcome: "AmountMismatch", defaultTransferType: "in", now, cancel);
            }

            if (invoice.Status == InvoiceStatus.Paid)
            {
                logger.LogInformation("Hóa đơn {InvoiceCode} đã được thanh toán trước đó.", invoice.Code);

                return await RecordOutcomeAsync(payload, invoiceCode, outcome: "AlreadyPaid", defaultTransferType: "in", now, cancel);
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

            await RecordOutcomeAsync(payload, invoiceCode, outcome: "Applied", defaultTransferType: "in", now, cancel);

            logger.LogInformation("Thanh toán thành công hóa đơn {InvoiceCode} qua SePay ({Amount} VND).",
                invoice.Code, payload.TransferAmount);

            return Result.Success();
        }, cancel);
    }

    private async Task<Result> RecordOutcomeAsync(
        SepayWebhookPayload payload,
        string? invoiceCode,
        string outcome,
        string defaultTransferType,
        DateTimeOffset now,
        CancellationToken cancel)
    {
        PaymentTransaction tx = PaymentTransaction.Record(
            payload.Id,
            payload.Gateway,
            payload.AccountNumber,
            payload.Content,
            payload.ReferenceCode,
            payload.TransferAmount,
            payload.TransferType ?? defaultTransferType,
            payload.TransactionDate,
            invoiceCode,
            outcome,
            now);

        await invoices.AddTransactionAsync(tx, cancel);
        await invoices.SaveChangesAsync(cancel);
        return Result.Success();
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
