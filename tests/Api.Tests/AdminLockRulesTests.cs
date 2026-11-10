using TrustSign.Common.Enums;
using TrustSign.Common.Errors;
using TrustSign.Domain.Entities.Identity;

namespace TrustSign.Api.Tests;

/// <summary>Quy tắc khóa / mở khóa của quản trị nền tảng (ADM-01, F14).</summary>
public sealed class AdminLockRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 9, 0, 0, TimeSpan.Zero);

    private static User ActiveUser()
    {
        User user = User.Register("a@chutin.vn", "Nguyễn Văn A", "hash", Now);
        user.MarkEmailVerified();
        return user;
    }

    [Fact]
    public void Locked_user_cannot_log_in_and_unlock_restores_access()
    {
        User user = ActiveUser();

        Assert.True(user.Lock("  Spam hợp đồng  ", Now).IsSuccess);
        Assert.Equal(UserStatus.Locked, user.Status);
        Assert.Equal("Spam hợp đồng", user.LockReason);
        Assert.Equal(IdentityErrors.AccountDisabled, user.CanLogin(Now).Error);
        Assert.Equal(AdminErrors.AlreadyLocked, user.Lock("lần hai", Now).Error);

        Assert.True(user.Unlock().IsSuccess);
        Assert.True(user.CanLogin(Now).IsSuccess);
        Assert.Null(user.LockReason);
        Assert.Equal(AdminErrors.NotLocked, user.Unlock().Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Lock_requires_a_reason(string? reason)
    {
        Assert.Equal(AdminErrors.LockReasonRequired, ActiveUser().Lock(reason, Now).Error);
        Assert.Equal(AdminErrors.LockReasonRequired, ActiveUser().Lock(new string('x', 501), Now).Error);
    }

    [Fact]
    public void Admin_and_unverified_accounts_cannot_be_locked()
    {
        User admin = User.CreateAdmin("root@chutin.vn", "Quản trị", "hash", Now);
        User pending = User.Register("p@chutin.vn", "Chưa xác minh", "hash", Now);

        Assert.Equal(AdminErrors.CannotLockAdmin, admin.Lock("x", Now).Error);
        Assert.Equal(AdminErrors.UserNotActive, pending.Lock("x", Now).Error);
        Assert.Equal(UserStatus.Pending, pending.Status);
    }

    [Fact]
    public void Tenant_lock_and_unlock()
    {
        Tenant tenant = Tenant.Create("CTY A", null, null, null, Guid.NewGuid(), Now);

        Assert.True(tenant.Lock("Nợ phí", Now).IsSuccess);
        Assert.Equal(TenantStatus.Locked, tenant.Status);
        Assert.Equal(Now, tenant.LockedAt);
        Assert.Equal(AdminErrors.AlreadyLocked, tenant.Lock("Nợ phí", Now).Error);

        Assert.True(tenant.Unlock().IsSuccess);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Null(tenant.LockedAt);
    }
}
