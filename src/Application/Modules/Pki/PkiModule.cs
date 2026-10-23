using Microsoft.Extensions.DependencyInjection;

namespace TrustSign.Application.Modules.Pki;

/// <summary>
/// Module PKI: Root CA và Issuing CA nội bộ, cấp chứng thư X.509 cho người ký, thu hồi chứng thư (F14).
/// Khóa bí mật của người ký được mã hóa và giữ tại máy chủ theo mô hình ký từ xa.
/// </summary>
public static class PkiModule
{
    public static IServiceCollection AddPkiModule(this IServiceCollection services) => services;
}
