namespace TrustSign.Application.Modules.Identity;

/// <summary>Tài khoản quản trị nền tảng tạo sẵn khi chạy ở môi trường Development.</summary>
public sealed class AdminSeedOptions
{
    public const string SectionName = "AdminSeed";

    public string? Email { get; init; }

    public string? Password { get; init; }

    public string FullName { get; init; } = "Quản trị viên";
}
