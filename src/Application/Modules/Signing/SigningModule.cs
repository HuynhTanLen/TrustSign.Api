using Microsoft.Extensions.DependencyInjection;

namespace TrustSign.Application.Modules.Signing;

/// <summary>
/// Module Signing: xác thực người ký bằng link có token và OTP email (F06), ký và nhúng chữ ký PAdES (F07),
/// từ chối ký (F08), hoàn tất với dấu số hệ thống và dấu thời gian (F10), xác minh hợp đồng (F12),
/// chữ ký mẫu của người dùng và tách chữ ký từ ảnh (F27).
/// </summary>
public static class SigningModule
{
    public static IServiceCollection AddSigningModule(this IServiceCollection services) => services;
}
