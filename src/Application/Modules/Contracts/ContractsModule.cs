using Microsoft.Extensions.DependencyInjection;

namespace TrustSign.Application.Modules.Contracts;

/// <summary>
/// Module Contract: tải PDF và tính mã băm (F02), đặt vị trí ký (F03), cấu hình người ký (F04), gửi ký (F05),
/// hủy và hết hạn (F09), bảng điều khiển (F13), mẫu hợp đồng (F21), soạn từ mẫu (F22), tra cứu (F23).
/// Giữ vòng đời Nháp → Chờ ký → Đang ký → Hoàn tất / Từ chối / Đã hủy / Hết hạn.
/// </summary>
public static class ContractsModule
{
    public static IServiceCollection AddContractsModule(this IServiceCollection services) => services;
}
