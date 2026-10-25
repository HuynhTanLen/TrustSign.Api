using TrustSign.Domain.Entities.Identity;
using TrustSign.Common;
using TrustSign.Common.Enums;
namespace TrustSign.Api.Tests;

public sealed class UserLoginLockTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 25, 10, 0, 0, TimeSpan.Zero);

    private static User NewActiveUser()
    {
        User user = User.Register("a@b.com", "A", "Hash", T0);
        user.MarkEmailVerified();
        return user;
    }
    
    [Fact]
    public void RecordFailedLogin_After4FailedAttempts_IsNotLocked()
    {
        User user = NewActiveUser();
        for(int i = 0; i<4 ; i++)
        {
            user.RecordFailedLogin(T0);
        }
        Assert.Equal(4, user.FailedLoginCount );
        Assert.Null(user.LockedUntil);
    }

    [Fact]
    public void RecordFailedLogin_After5FailedAttempts_LocksFor15Minutes()
    {
        User user = NewActiveUser();
        for(int i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(T0);
        }
        Assert.Equal(5, user.FailedLoginCount);
        Assert.Equal(T0.AddMinutes(15), user.LockedUntil);
    }

    [Fact]
    public void RecordFailedLogin_WhenAlreadyLocked_KeepsOriginalLockTime()
    {
        User user = NewActiveUser();
        for(int i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(T0);
        }
        user.RecordFailedLogin(T0.AddMinutes(5));
        Assert.Equal(6, user.FailedLoginCount);
        Assert.Equal(T0.AddMinutes(15), user.LockedUntil);
    }
    [Fact]
    public void CanLogin_WhenLocked_IsRejected()
    {
        User user = NewActiveUser();
        for(int i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(T0);
        }

        Result  result = user.CanLogin(T0.AddMinutes(10));

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.AccountLocked,result.Error);
    
    }

    [Fact]
    public void RecordSuccessfulLogin_AfterLockExpired_ResetsCounter()
    {
        User user = NewActiveUser();
        for(int i = 0; i < 5; i++) {user.RecordFailedLogin(T0);}

        Result canLogin = user.CanLogin(T0.AddMinutes(16));
        user.RecordSuccessfulLogin(T0.AddMinutes(16));

        Assert.True(canLogin.IsSuccess);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockedUntil);
    }

    [Fact]
    public void RecordFailedLogin_AfterLockExpired_StartsCountingFromOne()
    {
        User user = NewActiveUser();
        for(int i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(T0);
        }
        user.RecordFailedLogin(T0.AddMinutes(16));

        Assert.Equal(1, user.FailedLoginCount);
        Assert.Null(user.LockedUntil);
    }

    [Fact]
    public void RecordFailedLogin_AtExactLockExpiry_StartsNewCount()
    {
        User user = NewActiveUser();
        for(int i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(T0);
        }
        user.RecordFailedLogin(T0.AddMinutes(15));
        
        Assert.Equal(1, user.FailedLoginCount);
        Assert.Null(user.LockedUntil);
    }

    [Fact]
    public void TestName()
    {
        // Given
    
        // When
    
        // Then
    }

}
