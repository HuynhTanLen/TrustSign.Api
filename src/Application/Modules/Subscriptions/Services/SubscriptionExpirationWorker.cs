using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrustSign.Domain.IRepositories.Subscriptions;

namespace TrustSign.Application.Modules.Subscriptions;

/// <summary>
/// Worker chạy nền định kỳ quét và cập nhật trạng thái các thuê bao và hóa đơn chờ đã quá hạn (TC18).
/// </summary>
public sealed class SubscriptionExpirationWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<SubscriptionExpirationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, timeProvider, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DoWorkAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi xảy ra trong quá trình quét hết hạn thuê bao và hóa đơn.");
            }

            try
            {
                await Task.Delay(Interval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task DoWorkAsync(CancellationToken cancel)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        var subscriptions = scope.ServiceProvider.GetRequiredService<ISubscriptionRepository>();
        var invoices = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();

        DateTimeOffset now = timeProvider.GetUtcNow();
        int expiredSubs = await subscriptions.ExpireDueAsync(now, cancel);
        int expiredInvoices = await invoices.ExpireDueAsync(now, cancel);

        if (expiredSubs > 0 || expiredInvoices > 0)
        {
            logger.LogInformation(
                "Đã quét hết hạn: {ExpiredSubs} thuê bao, {ExpiredInvoices} hóa đơn chờ.",
                expiredSubs,
                expiredInvoices);
        }
    }
}
