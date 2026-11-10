using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrustSign.Domain.Repositories;
using TrustSign.Domain.Repositories.Identity;

namespace TrustSign.Api.Tests;

/// <summary>
/// Truy vấn danh sách quản trị gom nhiều bảng trong một câu LINQ. Test trỏ tới cổng Postgres không tồn tại:
/// EF phải dịch được sang SQL trước rồi mới lỗi kết nối; nếu LINQ không dịch được, lỗi là InvalidOperationException.
/// </summary>
public sealed class AdminDirectoryQueryTranslationTests
{
    private static EfAdminDirectoryRepository Repository() =>
        new(new TrustSignDbContext(new DbContextOptionsBuilder<TrustSignDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=1")
            .UseSnakeCaseNamingConvention()
            .Options));

    [Fact]
    public async Task List_queries_translate_to_sql()
    {
        CancellationToken cancel = TestContext.Current.CancellationToken;

        await AssertFailsOnlyAtConnect(() => Repository().ListTenantsAsync(cancel));
        await AssertFailsOnlyAtConnect(() => Repository().ListUsersAsync(cancel));
        await AssertFailsOnlyAtConnect(() => Repository().CountContractsByStatusAsync(Guid.NewGuid(), cancel));
    }

    // Npgsql bọc lỗi kết nối trong exception của execution strategy, nên tìm NpgsqlException trong chuỗi InnerException.
    private static async Task AssertFailsOnlyAtConnect(Func<Task> query)
    {
        Exception? ex = await Record.ExceptionAsync(query);
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            if (e is NpgsqlException)
            {
                return;
            }
        }

        Assert.Fail($"Truy vấn lỗi trước khi kết nối (không dịch được sang SQL?): {ex}");
    }
}
