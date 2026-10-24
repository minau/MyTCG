using System.ComponentModel.DataAnnotations;
using MyTCG.Contracts;
using MyTCG.Infrastructure;
using Xunit;
namespace MyTCG.UnitTests;
public sealed class SecurityTests
{
    [Theory]
    [InlineData("too-short")]
    [InlineData("")]
    public void JwtKeyMustHaveAtLeast32Bytes(string key) => Assert.Throws<InvalidOperationException>(() => new JwtSettings { Key = key }.Validate());
    [Fact] public void RefreshTokensUseDeterministicSha256NotPlainText() {
        var hash = AccountService.Hash("secret"); Assert.Equal(64, hash.Length); Assert.NotEqual("secret", hash);
        Assert.Equal(hash, AccountService.Hash("secret")); Assert.NotEqual(hash, AccountService.Hash("another"));
    }
    [Theory]
    [InlineData("a", "valid@example.com")]
    [InlineData("valid_user", "invalid")]
    [InlineData("has spaces", "valid@example.com")]
    public void InvalidRegistrationFieldsAreRejected(string username, string email) {
        var request = new RegisterRequest(username, email, "Strong-password123!", "Strong-password123!");
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }
}
