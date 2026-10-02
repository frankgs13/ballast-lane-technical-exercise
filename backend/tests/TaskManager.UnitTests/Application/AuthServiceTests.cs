using Microsoft.Extensions.Time.Testing;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Auth;
using TaskManager.Application.Common;
using TaskManager.Domain.Common;
using TaskManager.Domain.Users;

namespace TaskManager.UnitTests.Application;

public class AuthServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 18, 0, 0, TimeSpan.Zero);
    private const string ValidPassword = "Sup3rSecret!";

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _hasher.Hash(Arg.Any<string>()).Returns(ci => $"hashed:{ci.Arg<string>()}");
        _hasher.Verify(Arg.Any<string?>(), Arg.Any<string>())
            .Returns(ci => ci.ArgAt<string?>(0) == $"hashed:{ci.ArgAt<string>(1)}");
        _tokens.CreateToken(Arg.Any<User>())
            .Returns(new AccessToken("jwt-token", Now.UtcDateTime.AddHours(1)));

        _sut = new AuthService(_users, _hasher, _tokens, new FakeTimeProvider(Now));
    }

    [Fact]
    public async Task RegisterAsync_WithValidRequest_StoresHashedPasswordAndReturnsToken()
    {
        var result = await _sut.RegisterAsync(
            new RegisterRequest("francisco", "francisco@example.com", ValidPassword), CancellationToken.None);

        await _users.Received(1).AddAsync(
            Arg.Is<User>(u => u.Username == "francisco" && u.PasswordHash == $"hashed:{ValidPassword}"),
            Arg.Any<CancellationToken>());
        Assert.Equal("jwt-token", result.AccessToken);
        Assert.Equal("francisco", result.User.Username);
        Assert.Equal("francisco@example.com", result.User.Email);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Short1")]
    [InlineData("alllowercase1")]
    [InlineData("ALLUPPERCASE1")]
    [InlineData("NoDigitsHere")]
    public async Task RegisterAsync_WithWeakPassword_ThrowsValidation(string? password)
    {
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
            _sut.RegisterAsync(new RegisterRequest("francisco", "francisco@example.com", password),
                CancellationToken.None));

        Assert.True(ex.Errors.ContainsKey("password"));
        await _users.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task RegisterAsync_WithSeveralInvalidFields_ReportsAllOfThem()
    {
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
            _sut.RegisterAsync(new RegisterRequest("x", "bad-email", "weak"), CancellationToken.None));

        Assert.True(ex.Errors.ContainsKey("username"));
        Assert.True(ex.Errors.ContainsKey("email"));
        Assert.True(ex.Errors.ContainsKey("password"));
    }

    [Fact]
    public async Task RegisterAsync_WithInvalidData_DoesNotHashPassword()
    {
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            _sut.RegisterAsync(new RegisterRequest("x", "bad-email", ValidPassword), CancellationToken.None));

        _hasher.DidNotReceiveWithAnyArgs().Hash(default!);
    }

    [Fact]
    public async Task RegisterAsync_WithTakenUsername_ThrowsConflict()
    {
        _users.UsernameExistsAsync("francisco", Arg.Any<CancellationToken>()).Returns(true);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.RegisterAsync(new RegisterRequest("francisco", "new@example.com", ValidPassword),
                CancellationToken.None));

        Assert.Equal("username", ex.Field);
        await _users.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task RegisterAsync_WithTakenEmail_ThrowsConflict()
    {
        _users.EmailExistsAsync("taken@example.com", Arg.Any<CancellationToken>()).Returns(true);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.RegisterAsync(new RegisterRequest("francisco", "Taken@Example.com", ValidPassword),
                CancellationToken.None));

        Assert.Equal("email", ex.Field);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsToken()
    {
        var user = User.Create("francisco", "francisco@example.com", $"hashed:{ValidPassword}", Now.UtcDateTime);
        _users.GetByUsernameAsync("francisco", Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.LoginAsync(new LoginRequest("francisco", ValidPassword), CancellationToken.None);

        Assert.Equal("jwt-token", result.AccessToken);
        Assert.Equal(user.Id, result.User.Id);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ThrowsInvalidCredentials()
    {
        var user = User.Create("francisco", "francisco@example.com", $"hashed:{ValidPassword}", Now.UtcDateTime);
        _users.GetByUsernameAsync("francisco", Arg.Any<CancellationToken>()).Returns(user);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _sut.LoginAsync(new LoginRequest("francisco", "WrongPass1"), CancellationToken.None));

        _tokens.DidNotReceiveWithAnyArgs().CreateToken(default!);
    }

    [Fact]
    public async Task LoginAsync_WithUnknownUser_StillVerifiesAPasswordToAvoidTimingAttacks()
    {
        _users.GetByUsernameAsync("ghost", Arg.Any<CancellationToken>()).Returns((User?)null);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _sut.LoginAsync(new LoginRequest("ghost", ValidPassword), CancellationToken.None));

        _hasher.Received(1).Verify(null, ValidPassword);
    }

    [Theory]
    [InlineData(null, "pwd")]
    [InlineData("user", null)]
    [InlineData(" ", " ")]
    public async Task LoginAsync_WithMissingFields_ThrowsValidation(string? username, string? password)
    {
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            _sut.LoginAsync(new LoginRequest(username, password), CancellationToken.None));
    }

    [Fact]
    public async Task GetCurrentUserAsync_WhenUserExists_ReturnsProfile()
    {
        var user = User.Create("francisco", "francisco@example.com", "hash", Now.UtcDateTime);
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.GetCurrentUserAsync(user.Id, CancellationToken.None);

        Assert.Equal("francisco", result.Username);
    }

    [Fact]
    public async Task GetCurrentUserAsync_WhenUserMissing_ThrowsNotFound()
    {
        _users.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.GetCurrentUserAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
