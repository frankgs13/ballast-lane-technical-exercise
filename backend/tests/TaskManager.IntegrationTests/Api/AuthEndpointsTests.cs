using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using TaskManager.Application.Auth;
using TaskManager.Domain.Users;
using TaskManager.Infrastructure.Security;
using static TaskManager.IntegrationTests.Api.TaskManagerApiFactory;

namespace TaskManager.IntegrationTests.Api;

public sealed class AuthEndpointsTests(TaskManagerApiFactory factory) : IClassFixture<TaskManagerApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_IsPublic()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithValidData_Returns201WithTokenAndProfile()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("newuser", "newuser@example.com", "Sup3rSecret!"), Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.Equal("newuser", body.User.Username);
    }

    [Fact]
    public async Task Register_ResponseNeverContainsPasswordOrHash()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("secretive", "secretive@example.com", "Sup3rSecret!"), Json);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Sup3rSecret!", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_WithInvalidData_Returns400WithErrorsPerField()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("x", "not-an-email", "weak"), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ReadErrorsAsync(response);
        Assert.Contains("username", errors);
        Assert.Contains("email", errors);
        Assert.Contains("password", errors);
    }

    [Fact]
    public async Task Register_WithTakenUsername_Returns409()
    {
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("duplicate", "first@example.com", "Sup3rSecret!"), Json);

        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("DUPLICATE", "second@example.com", "Sup3rSecret!"), Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("username", await ReadErrorsAsync(response));
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithToken()
    {
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("loginuser", "loginuser@example.com", "Sup3rSecret!"), Json);

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("loginuser", "Sup3rSecret!"), Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
    }

    [Theory]
    [InlineData("loginuser2", "WrongPass1")]
    [InlineData("nobody", "Sup3rSecret!")]
    public async Task Login_WithBadCredentials_Returns401WithGenericMessage(string username, string password)
    {
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("loginuser2", "loginuser2@example.com", "Sup3rSecret!"), Json);

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password), Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid username or password", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithToken_ReturnsCurrentUser()
    {
        var (client, auth) = await factory.CreateAuthenticatedClientAsync("meuser");

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<UserResponse>(Json);
        Assert.Equal(auth.User.Id, me!.Id);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithTamperedToken_Returns401()
    {
        var (_, auth) = await factory.CreateAuthenticatedClientAsync();
        var tampered = auth.AccessToken[..^4] + "abcd";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tampered);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithExpiredToken_Returns401()
    {
        var issuedLongAgo = new FakeTimeProvider(DateTimeOffset.UtcNow.AddDays(-2));
        var tokenService = new JwtTokenService(Options.Create(new JwtOptions
        {
            Issuer = Issuer, Audience = Audience, SigningKey = SigningKey, ExpirationMinutes = 60,
        }), issuedLongAgo);
        var token = tokenService.CreateToken(User.Create("expired", "expired@example.com", "hash", DateTime.UtcNow));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/tasks");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<string[]> ReadErrorsAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return [.. document.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name)];
    }
}
