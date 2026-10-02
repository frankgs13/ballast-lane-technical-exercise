using TaskManager.Domain.Common;
using TaskManager.Domain.Users;

namespace TaskManager.UnitTests.Domain;

public class UserTests
{
    private static readonly DateTime NowUtc = new(2026, 10, 2, 18, 0, 0, DateTimeKind.Utc);
    private const string Hash = "hashed-password";

    [Fact]
    public void Create_WithValidData_NormalizesValues()
    {
        var user = User.Create("  Francisco  ", "  Francisco@Example.COM ", Hash, NowUtc);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("Francisco", user.Username);
        Assert.Equal("francisco@example.com", user.Email);
        Assert.Equal(Hash, user.PasswordHash);
        Assert.Equal(NowUtc, user.CreatedAtUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("user name")]
    [InlineData("user<script>")]
    public void Create_WithInvalidUsername_Throws(string? username)
    {
        var ex = Assert.Throws<DomainValidationException>(
            () => User.Create(username, "user@example.com", Hash, NowUtc));

        Assert.True(ex.Errors.ContainsKey("username"));
    }

    [Fact]
    public void Create_WithUsernameTooLong_Throws()
    {
        var ex = Assert.Throws<DomainValidationException>(
            () => User.Create(new string('a', User.UsernameMaxLength + 1), "user@example.com", Hash, NowUtc));

        Assert.True(ex.Errors.ContainsKey("username"));
    }

    [Theory]
    [InlineData("john.doe")]
    [InlineData("john_doe-99")]
    public void Create_WithAllowedUsernameCharacters_Succeeds(string username)
    {
        var user = User.Create(username, "user@example.com", Hash, NowUtc);

        Assert.Equal(username, user.Username);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("missing@tld")]
    [InlineData("two@@example.com")]
    public void Create_WithInvalidEmail_Throws(string? email)
    {
        var ex = Assert.Throws<DomainValidationException>(
            () => User.Create("john", email, Hash, NowUtc));

        Assert.True(ex.Errors.ContainsKey("email"));
    }

    [Fact]
    public void Create_WithoutPasswordHash_Throws()
    {
        Assert.Throws<ArgumentException>(() => User.Create("john", "john@example.com", " ", NowUtc));
    }
}
