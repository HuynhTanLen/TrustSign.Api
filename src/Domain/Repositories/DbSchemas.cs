namespace TrustSign.Domain.Repositories;

/// <summary>
/// Mỗi module một schema PostgreSQL trong cùng một database. Bảng của module nào nằm trong schema của module đó,
/// nhờ vậy ranh giới module thấy được ngay trong database mà các thao tác vẫn chung được một transaction.
/// </summary>
public static class DbSchemas
{
    public const string Identity = "identity";
    public const string Subscription = "subscription";
    public const string Contract = "contract";
    public const string Signing = "signing";
    public const string Pki = "pki";
    public const string Audit = "audit";
    public const string Notification = "notification";
}
