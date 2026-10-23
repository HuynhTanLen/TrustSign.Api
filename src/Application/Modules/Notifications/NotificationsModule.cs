using Microsoft.Extensions.DependencyInjection;

namespace TrustSign.Application.Modules.Notifications;

/// <summary>
/// Module Notification: gửi email qua SMTP (F24) gồm xác minh tài khoản, lời mời thành viên, link mời ký, mã OTP,
/// kết quả từ chối, bản hợp đồng cuối và nhắc gói sắp hết hạn.
/// </summary>
public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services) => services;
}
