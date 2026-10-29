using TrustSign.Common;

namespace TrustSign.Domain.IRepositories;

/// <summary>
/// Gom thao tác của nhiều module vào một transaction database (vd tạo doanh nghiệp + cấp gói dùng thử,
/// gửi ký + trừ hạn mức). Chỉ commit khi kết quả thành công; lỗi nghiệp vụ hoặc exception đều rollback.
/// Nếu đang ở trong một transaction thì chạy luôn trong transaction đó.
/// </summary>
public interface IUnitOfWork
{
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken cancel)
        where TResult : Result;
}
