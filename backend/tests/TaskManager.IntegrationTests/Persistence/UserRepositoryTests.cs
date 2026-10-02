using TaskManager.Application.Common;
using TaskManager.Domain.Users;
using TaskManager.Infrastructure.Persistence;

namespace TaskManager.IntegrationTests.Persistence;

[Collection(SqlServerGroup.Name)]
public sealed class UserRepositoryTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime NowUtc = new(2026, 10, 2, 18, 0, 0, DateTimeKind.Utc);
    private readonly UserRepository _sut = new(fixture.CreateConnectionFactory());

    public async Task InitializeAsync()
    {
        if (fixture.UnavailableReason is null)
        {
            await fixture.ResetAsync();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [SkippableFact]
    public async Task AddAsync_ThenGetById_RoundTripsAllFields()
    {
        Skip.If(fixture.UnavailableReason is not null, fixture.UnavailableReason);
        var user = User.Create("francisco", "francisco@example.com", "hash-value", NowUtc);

        await _sut.AddAsync(user, CancellationToken.None);
        var loaded = await _sut.GetByIdAsync(user.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(user.Username, loaded.Username);
        Assert.Equal(user.Email, loaded.Email);
        Assert.Equal(user.PasswordHash, loaded.PasswordHash);
        Assert.Equal(NowUtc, loaded.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, loaded.CreatedAtUtc.Kind);
    }

    [SkippableFact]
    public async Task GetByUsernameAsync_IsCaseInsensitive()
    {
        Skip.If(fixture.UnavailableReason is not null, fixture.UnavailableReason);
        var user = User.Create("Francisco", "francisco@example.com", "hash", NowUtc);
        await _sut.AddAsync(user, CancellationToken.None);

        var loaded = await _sut.GetByUsernameAsync("FRANCISCO", CancellationToken.None);

        Assert.Equal(user.Id, loaded?.Id);
    }

    [SkippableFact]
    public async Task GetByUsernameAsync_WhenMissing_ReturnsNull()
    {
        Skip.If(fixture.UnavailableReason is not null, fixture.UnavailableReason);

        Assert.Null(await _sut.GetByUsernameAsync("nobody", CancellationToken.None));
    }

    [SkippableFact]
    public async Task ExistsQueries_ReflectStoredData()
    {
        Skip.If(fixture.UnavailableReason is not null, fixture.UnavailableReason);
        var user = User.Create("francisco", "francisco@example.com", "hash", NowUtc);
        await _sut.AddAsync(user, CancellationToken.None);

        Assert.True(await _sut.ExistsAsync(user.Id, CancellationToken.None));
        Assert.False(await _sut.ExistsAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.True(await _sut.UsernameExistsAsync("francisco", CancellationToken.None));
        Assert.False(await _sut.UsernameExistsAsync("someone", CancellationToken.None));
        Assert.True(await _sut.EmailExistsAsync("francisco@example.com", CancellationToken.None));
        Assert.False(await _sut.EmailExistsAsync("other@example.com", CancellationToken.None));
    }

    [SkippableFact]
    public async Task AddAsync_WithDuplicateUsername_ThrowsConflictForUsername()
    {
        Skip.If(fixture.UnavailableReason is not null, fixture.UnavailableReason);
        await _sut.AddAsync(User.Create("francisco", "a@example.com", "hash", NowUtc), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.AddAsync(User.Create("FRANCISCO", "b@example.com", "hash", NowUtc), CancellationToken.None));

        Assert.Equal("username", ex.Field);
    }

    [SkippableFact]
    public async Task AddAsync_WithDuplicateEmail_ThrowsConflictForEmail()
    {
        Skip.If(fixture.UnavailableReason is not null, fixture.UnavailableReason);
        await _sut.AddAsync(User.Create("first", "same@example.com", "hash", NowUtc), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.AddAsync(User.Create("second", "same@example.com", "hash", NowUtc), CancellationToken.None));

        Assert.Equal("email", ex.Field);
    }

    [SkippableFact]
    public async Task GetByUsernameAsync_WithSqlInjectionPayload_IsTreatedAsPlainText()
    {
        Skip.If(fixture.UnavailableReason is not null, fixture.UnavailableReason);
        await _sut.AddAsync(User.Create("francisco", "francisco@example.com", "hash", NowUtc), CancellationToken.None);

        var loaded = await _sut.GetByUsernameAsync("' OR 1=1 --", CancellationToken.None);

        Assert.Null(loaded);
    }
}
