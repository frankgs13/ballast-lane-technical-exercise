using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TaskManager.Domain.Users;
using TaskManager.Infrastructure.Security;

namespace TaskManager.UnitTests.Infrastructure;

public class JwtTokenServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 18, 0, 0, TimeSpan.Zero);

    private readonly JwtOptions _options = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = "unit-test-signing-key-that-is-at-least-32-bytes-long",
        ExpirationMinutes = 30,
    };

    private readonly User _user = User.Create("francisco", "francisco@example.com", "hash", Now.UtcDateTime);

    [Fact]
    public void CreateToken_ExpiresAfterConfiguredMinutes()
    {
        var sut = new JwtTokenService(Options.Create(_options), new FakeTimeProvider(Now));

        var token = sut.CreateToken(_user);

        Assert.Equal(Now.UtcDateTime.AddMinutes(30), token.ExpiresAtUtc);
    }

    [Fact]
    public async Task CreateToken_ProducesSignedTokenWithUserClaims()
    {
        var sut = new JwtTokenService(Options.Create(_options), new FakeTimeProvider(Now));

        var token = sut.CreateToken(_user);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            ValidateLifetime = false,
        });

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal(_user.Id.ToString(), result.Claims[JwtRegisteredClaimNames.Sub]);
        Assert.Equal("francisco", result.Claims[JwtRegisteredClaimNames.UniqueName]);
        Assert.Equal("francisco@example.com", result.Claims[JwtRegisteredClaimNames.Email]);
    }

    [Fact]
    public async Task CreateToken_SignedWithDifferentKey_FailsValidation()
    {
        var sut = new JwtTokenService(Options.Create(_options), new FakeTimeProvider(Now));

        var token = sut.CreateToken(_user);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("a-completely-different-signing-key-32-bytes!!")),
            ValidateLifetime = false,
        });

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("too-short")]
    [InlineData("")]
    public void JwtOptions_WithWeakSigningKey_IsInvalid(string key)
    {
        var options = new JwtOptions { Issuer = "i", Audience = "a", SigningKey = key, ExpirationMinutes = 10 };

        Assert.False(options.IsValid());
    }

    [Fact]
    public void JwtOptions_WithNonPositiveExpiration_IsInvalid()
    {
        var options = new JwtOptions
        {
            Issuer = "i", Audience = "a", SigningKey = _options.SigningKey, ExpirationMinutes = 0,
        };

        Assert.False(options.IsValid());
    }
}
