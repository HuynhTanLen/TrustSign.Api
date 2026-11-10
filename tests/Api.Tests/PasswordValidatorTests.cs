using TrustSign.Common.Validation;

namespace TrustSign.Api.Tests;

public sealed class PasswordValidatorTests
{
    [Theory]
    [InlineData("TrustSign@2026", true)]
    [InlineData("abc1234", false)]       // 7 ký tự
    [InlineData("abcdefgh", false)]      // thiếu số
    [InlineData("12345678", false)]      // thiếu chữ
    [InlineData("Password1", false)]     // phổ biến
    [InlineData("an.nguyen2026", false)] // chứa phần trước @ của email
    [InlineData("NguyenVanAn99", false)] // chứa họ tên
    public void Checks_rules(string password, bool expected) =>
        Assert.Equal(expected, PasswordValidator.IsAcceptable(password, "an.nguyen@trustsign.vn", "Nguyen Van An"));

    [Fact]
    public void Rejects_password_longer_than_max() =>
        Assert.False(PasswordValidator.IsAcceptable(new string('a', 64) + "1", "x@y.vn", "Tên"));

    [Fact]
    public void Accepts_password_at_max_length() =>
        Assert.True(PasswordValidator.IsAcceptable(new string('a', 63) + "1", "x@y.vn", "Tên"));
}
