using Microsoft.Extensions.DependencyInjection;

namespace TrustSign.Application.Modules.Subscriptions;

/// <summary>
/// Module Subscription: gói dịch vụ (F17), đăng ký và gia hạn thuê bao (F18), thanh toán qua SePay (F19),
/// hạn mức hợp đồng gửi ký mỗi tháng (F20). Module Contract gọi interface hạn mức của module này khi gửi ký.
/// </summary>
public static class SubscriptionsModule
{
    public static IServiceCollection AddSubscriptionsModule(this IServiceCollection services)
    {
        services.AddOptions<SepayOptions>()
            .BindConfiguration(SepayOptions.SectionName);

        services.AddScoped<IPlanSeeder, PlanSeeder>();
        services.AddScoped<ISubscriptionProvisioner, SubscriptionProvisioner>();
        services.AddScoped<IPlanService, PlanService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<ISepayWebhookService, SepayWebhookService>();
        services.AddScoped<IQuotaService, QuotaService>();
        services.AddScoped<IInvoicePdfService, InvoicePdfService>();

        services.AddHostedService<SubscriptionExpirationWorker>();

        return services;
    }
}
