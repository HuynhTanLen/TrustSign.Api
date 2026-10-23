using Microsoft.Extensions.DependencyInjection;

namespace TrustSign.Application.Modules.Audit;

/// <summary>
/// Module Audit: nhật ký kiểm toán dạng chuỗi băm (F11), mỗi sự kiện lưu mã băm của sự kiện trước;
/// kiểm tra chuỗi cho trang xác minh (F12) và cho quản trị viên xem nhật ký (F14).
/// </summary>
public static class AuditModule
{
    public static IServiceCollection AddAuditModule(this IServiceCollection services) => services;
}
